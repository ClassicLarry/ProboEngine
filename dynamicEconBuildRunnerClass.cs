using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Configuration;
using MathNet.Numerics.LinearAlgebra;

namespace ProboEngine_Stand_Alone_Version
{
	public class dynamicEconBuildRunnerClass
    {
        private double lastUnitDuration { get; set; }
		private bool usePreprogrammedBuild { get; set; }
        private double[] economicVarsToLog { get; set; }
        private bool chronoFirstProbe { get; set; }
        private bool finalIterationLogNNData { get; set; }
        public bool coefFailed { get; set; }
        public double timeDelayed { get; set; }
        public List<buildOrderEntry> chronoWGRLog { get; set; }
        public List<buildOrderEntry> chronoCELog { get; set; }
        public List<buildOrderEntry> chronoEcoLog { get; set; }

        public timeDelayedTrackerObject timeDelayedTracker { get; set; }
        public double endTime { get; set; }
        public double mineralBank { get; set; }
        public double gasBank { get; set; }
        public int currentSupply { get; set; }
        public int currentSupplyCap { get; set; }
        public double mineralIncome { get; set; }
        public double gasIncome { get; set; }
        public double currentTime { get; set; }
        public buildingStatusTrackerObject buildingStatusTracker { get; set; }

        public double[] prVals { get; set; }
        public double[] aVals { get; set; }
        public double[] eVals { get; set; }
        public double[] pyVals { get; set; }
        public double[] nVals { get; set; }
        public List<buildOrderEntry> unfinishedEventLog { get; set; }

        public List<bUUDataObject> pairObjectsLibrary { get; set; }
        public List<buildOrderEntry> realizedBuildLog { get; set; }
		public List<buildOrderEntry> originalNoEconBuild { get; set; }
        public List<ProcessFriendlyBuildObject> SlimBuild { get; set; }
        public List<ProcessFriendlyBuildObject> simulatedSlimBuild { get; set; }
        public List<BankStatement> bankStatementList { get; set; }
        public List<double> timeChronoAvailable { get; set; }
        public bool recordBankStatements { get; set; }
        private bool useNNForDecision { get; set; }

        public dynamicEconBuildRunnerClass(buildCreatorPreEcon buildDetails, List<buildOrderEntry> buildOrderInput, coefficientPackage thisCoefficientPackage, bool myRecordBankStatements, bool myFinalIterationLogNNData = false, bool myUseNNForDecision = false, List<buildOrderEntry> preProgrammedBuild = null)
        {
            useNNForDecision = myUseNNForDecision;
			usePreprogrammedBuild = false;
			if (preProgrammedBuild != null) { usePreprogrammedBuild = true; }
			originalNoEconBuild = buildOrderInput;
            lastUnitDuration = buildDetails.lastEventDuration;
			finalIterationLogNNData = myFinalIterationLogNNData;
            
			//if true, make probes made between 36 & 56 seconds produce at 1.5 speed
            chronoFirstProbe = (buildDetails.chronoEcoLog.Count() > 0);
            //use incase coefficients create impossible situation
            coefFailed = false;
            //perform build order and identify end time
            //each item will perform once resources are available and (minTime+delay) is met
            //need a List of events to complete (pylons, probes, nexi, assimilators)
            //whenever one of these events complete, update income, supply cap
            pairObjectsLibrary = buildDetails.pairObjectsLibrary;
            chronoCELog = buildDetails.chronoCELog;
            chronoWGRLog = buildDetails.chronoWGLog;
            chronoEcoLog = buildDetails.chronoEcoLog;

            //create more access friendly storage object
            //need to access mineralCost, gasCost, supplyCost
            initializeBuildConditions(12, 1, 0, 0, 1, 1);
            initializeSlimBuild(buildOrderInput);
            initializeCoefficients(thisCoefficientPackage);
            initializeTimeDelayedTracker();
            initializeBankStatements(myRecordBankStatements);
            initializeTimeChronoAvailable();

            //need to use economic model to insert economy on the fly
            dynamicEconomicInsertion(buildOrderInput, preProgrammedBuild);
            if (coefFailed)
            {
                endTime = 1000000000; return;
            }
            updateEndTime(buildDetails.lastEventDuration);
            //add extra bank statements if neccessary
            if (myRecordBankStatements)
            {
                //update by 3 times current list->update to 1000 for now
                int amountToAdd = 1000 - bankStatementList.Count();
                if (amountToAdd > 0)
                {
                    updateBankStatements(amountToAdd);
                }
            }
        }
        private void initializeTimeChronoAvailable()
        {
            //chrono boost costs 50 energy, and a nexus regains energy at .7875 energy/s
            //nexus starts with 50 energy
            int startingEnergy = 50;
            double regenRate = 0.7875;
            int costToChrono = 50;
            //initialize chrono boost times for first nexus
            //do all times up to 10000 seconds
            timeChronoAvailable = new List<double>();
            for (int i = 0; i < 10000; i++)
            {
                double newTime = i * costToChrono / regenRate;
                timeChronoAvailable.Add(newTime);
                if (newTime > 10000) { break; }
            }
        }
        private void addNexusToTimeChronoAvailable(double nexusCompletionTime)
        {
            double regenRate = 0.7875;
            int costToChrono = 50;
            //find first entrySpot based on nexusCompletionTime
            int firstEntrySpot = 0;
            for (int x = 1; x < timeChronoAvailable.Count(); x++)
            {
                if (timeChronoAvailable[x] > nexusCompletionTime)
                {
                    firstEntrySpot = x;
                    break;
                }
            }
            for (int i = 0; i < 10000; i++)
            {
                double newTime = i * costToChrono / regenRate + nexusCompletionTime;
                if (newTime > 10000) { break; }

                //insert times based on spot
                timeChronoAvailable.Insert(firstEntrySpot + buildingStatusTracker.nexusCount * i, newTime);

            }
        }
        private void initializeBankStatements(bool myRecordBankStatements)
        {
            if (myRecordBankStatements)
            {
                bankStatementList = new List<BankStatement>();
                recordBankStatements = true;
            }
            else
            {
                recordBankStatements = false;
            }
        }
        private void updateEndTime(double lastEventDuration)
        {
            endTime = currentTime + lastEventDuration;
        }
        public void dynamicEconomicInsertion(List<buildOrderEntry> buildOrderInput, List<buildOrderEntry> preprogrammedBuildOrder)
        {
            string name = "";
            double minTime = 0;
			int spotInPreprogrammedBuild = 0;
            //perform loop for each thing to build, allowing for economic insertion at each point
            for (int x = 0; x < buildOrderInput.Count(); x++)
            {
                for (int i = 0; i < 1000; i++)
                {
					if (usePreprogrammedBuild) {
						name = preprogrammedBuildOrder[spotInPreprogrammedBuild].itemToBuild;
						spotInPreprogrammedBuild++;
					}
					else { name = economicDecisionProcess(x); }

						//if recordBankStatements, record data matrix and decision made
				    if (finalIterationLogNNData)
                    {
						//Removed this for public copy of code
                        //logDecision(name, x);
                    }
                    if (!name.Equals(SlimBuild[x].name))
                    {
                        realizedBuildLog.Add(performAction(name, 0, buildOrderInput, 0));
                        if (coefFailed) { return; }
                        if (realizedBuildLog.Count() > 200) { coefFailed = true; return; }
                    }
                    else { break; }
                }
                minTime = SlimBuild[x].minTime;
                realizedBuildLog.Add(performAction(name, minTime, buildOrderInput, 0));
                if (coefFailed) { return; }
                if (realizedBuildLog.Count() > 200) { coefFailed = true; return; }
            }
        }
		
        private string getEconFeatures(int x)
        {
			//get all features that will be input to neural network
            StringBuilder econData = new StringBuilder();
            //add 8 state variables
            econData.Append(mineralBank.ToString() + "," + gasBank.ToString() + "," + buildingStatusTracker.futureNexusCount + "," + buildingStatusTracker.futureProbeCount + "," + buildingStatusTracker.futureAssimilatorCount + "," + buildingStatusTracker.freeNexusCount + "," + x + "," + currentSupply + "," + currentSupplyCap);
            double[] options = new double[4];
            options[0] = 0; //pylon
            options[1] = 1; //nexus
            options[2] = 2; //probe
            options[3] = 3; //assimilator

            //test pylon
            updateSimulatedSlimBuildWithEcoOption(options, 0, x);
            econData.Append(getNexusFeatures(x));
            econData.Append(getProbeFeatures(x));
            econData.Append(getAssimilatorFeatures(x));
            simulatedSlimBuild.RemoveAt(x);

            //now test nexus
            updateSimulatedSlimBuildWithEcoOption(options, 1, x);
            econData.Append(getPylonFeatures(x));
            econData.Append(getProbeFeatures(x));
            econData.Append(getAssimilatorFeatures(x));
            simulatedSlimBuild.RemoveAt(x);

            //now test probe
            updateSimulatedSlimBuildWithEcoOption(options, 2, x);
            econData.Append(getPylonFeatures(x));
            econData.Append(getNexusFeatures(x));
            econData.Append(getAssimilatorFeatures(x));
            simulatedSlimBuild.RemoveAt(x);

            //now test assimilator
            updateSimulatedSlimBuildWithEcoOption(options, 3, x);
            econData.Append(getPylonFeatures(x));
            econData.Append(getNexusFeatures(x));
            econData.Append(getProbeFeatures(x));
            simulatedSlimBuild.RemoveAt(x);
            return econData.ToString();
        }
        private string getPylonFeatures(int x)
        {
            double p0 = calcPylonBottleneckDifferentialTerm(x);
            return "," + p0.ToString();
        }

        private string getProbeFeatures(int x)
        {
            double e0 = simulatedSlimBuild[simulatedSlimBuild.Count - 1].minTime + timeDelayed + lastUnitDuration - currentTime;
            double[] probeTerms = calcProbePayoffToLog(x, e0);
            string toReturn = "";
            for (int i = 0; i < probeTerms.Count(); i++)
            {
                toReturn = toReturn + "," + probeTerms[i].ToString();
            }
            return toReturn;
        }

        private string getNexusFeatures(int x)
        {
            double e0 = simulatedSlimBuild[simulatedSlimBuild.Count - 1].minTime + timeDelayed + lastUnitDuration - currentTime;
            double[] nexusTerms = calcNexusPayoffTermsToLog(x, e0);
            string toReturn = "";
            for (int i = 0; i < nexusTerms.Count(); i++)
            {
                toReturn = toReturn + "," + nexusTerms[i].ToString();
            }
            return toReturn;
        }
        private string getAssimilatorFeatures(int x)
        {
            int numExtraNexi = 0;
            foreach (buildOrderEntry thisEntry in unfinishedEventLog)
            {
                if (thisEntry.itemToBuild == "nexus" && thisEntry.timeToBuild < currentTime + 21)
                {
                    numExtraNexi++;
                }
            }
            double a0 = 0;
            double a1 = 0;
            double a2 = 0;
            if ((buildingStatusTracker.nexusCount + numExtraNexi) * 2 > buildingStatusTracker.futureAssimilatorCount)
            {
                aVals = new double[3];
                aVals[1] = 1;
                aVals[2] = 1;
                a0 = calcAssimilatorDelayDifferential(x, 1);
                a1 = calcAssimilatorDelayDifferential(x, 5);
                a2 = calcAssimilatorDelayDifferential(x, 10);
            }
            string toReturn = "," + a0 + "," + a1 + "," + a2;
            return toReturn;
        }

        public string economicDecisionProcess(int x)
        {
            //value to return
            string name = "";
			if (realizedBuildLog.Count() == 1)
			{
				int i = 0;
			}
            //if nn iteration
            if (useNNForDecision)
            {
                //add in neural network part
                double[,] features = getNNFeaturesEventVsEcon(x);
                NNParams newParams = new NNParams();
                Matrix<double> featuresMatrix = Matrix<double>.Build.DenseOfArray(features);
                double[] results = newParams.decisionRanking(featuresMatrix);
                if (results[0] > 0)
                {
                    name = SlimBuild[x].name; return name;
                }
                //only reaches here if economy was chosen
                double[,] econFeatures = getNNFeaturesEconOnly(x);
                Matrix<double> econFeaturesMatrix = Matrix<double>.Build.DenseOfArray(econFeatures);
                double[] econResults = newParams.econDecisionRanking(econFeaturesMatrix);
                string NNEconResult = findBestEconOption(econResults);
                return NNEconResult;
            }

            //factors to calculate
            double valuePylon;
            double valueNexus;
            double valueProbe;
            double valueAssimilator;
            double valueEvent;
            simulatedSlimBuild = new List<ProcessFriendlyBuildObject>(SlimBuild);

            double eventGenTermNew = simulatedSlimBuild[simulatedSlimBuild.Count - 1].minTime + timeDelayed + lastUnitDuration - currentTime;
            double pylonPayOffNew = calcPylonBottleneckDifferentialTerm(x);
            double nexusPayOffNew = calcNexusPayoffTermNew(x, eventGenTermNew)/400 * nVals[0];
            double assimilatorPayOffNew = calcAssimilatorPayoffTermNew(x)/75 * aVals[0];
            double probePayOffNew = calcProbePayoffNew(x, eventGenTermNew)/50 * prVals[0];
            double eventPayOffNew = .02 * eVals[0];
            //if pylon is event, add numbers together 
            if (SlimBuild[x].name.Equals("pylon")) { eventPayOffNew = eventPayOffNew + pylonPayOffNew; }
            //var names
            valuePylon = pylonPayOffNew;
            valueNexus = nexusPayOffNew;
            valueProbe = probePayOffNew;
            valueAssimilator = assimilatorPayOffNew;
            valueEvent = eventPayOffNew;

            double[] options = new double[5] { valuePylon, valueNexus, valueProbe, valueAssimilator, valueEvent };
            double[] ecoOptions = new double[4] { valuePylon, valueNexus, valueProbe, valueAssimilator };
            double highestScore = options.Max();

            if (highestScore == valueEvent)
            {
                name = SlimBuild[x].name; return name;
            }
            else
            {
                updateSimulatedSlimBuildWithEcoOption(ecoOptions, highestScore, x); //add best option to simulatedSlimBuild
                name = findNameOfXBest(ecoOptions, 1);
            }
            //find new scores
            double newPylonValue = 0;
            double newNexusValue = 0;
            double newProbeValue = 0;
            double newAssimilatorValue = 0;
            if (!name.Equals("pylon"))
            {
                newPylonValue = calcPylonBottleneckDifferentialTerm(x);
            }
            if (!name.Equals("nexus"))
            {
                newNexusValue = calcNexusPayoffTermNew(x, eventGenTermNew) / 400 * nVals[0];
            }
            if (!name.Equals("Probe"))
            {
                newProbeValue = calcProbePayoffNew(x, eventGenTermNew) / 50 * prVals[0];
            }
            if (!name.Equals("assimilator"))
            {
                newAssimilatorValue = calcAssimilatorPayoffTermNew(x) / 75 * aVals[0];
            }

            //find best option from this list
            double[] newEcoOptions = new double[4] { newPylonValue, newNexusValue, newProbeValue, newAssimilatorValue };
            //now need to perform analysis on 2nd best against best, and best against second best
            //find score for second best
            double secondBestOptionNewScore = newEcoOptions.Max();
            string newOptionName = findNameOfXBest(newEcoOptions, 1);

            //now replace best choice with second best choice in simulatedSlimBuild
            simulatedSlimBuild.RemoveAt(x);
            updateSimulatedSlimBuildWithEcoOption(newEcoOptions, secondBestOptionNewScore, x);
            //find how firstBest
            double oldRank1NewScore;
            if (highestScore == valueProbe)
            {
                oldRank1NewScore = calcProbePayoffNew(x, eventGenTermNew) / 50;
            }
            else if (highestScore == valueNexus)
            {
                oldRank1NewScore = calcNexusPayoffTermNew(x, eventGenTermNew) / 400;
            }
            else if (highestScore == valuePylon)
            {
                oldRank1NewScore = calcPylonBottleneckDifferentialTerm(x);
            }
            else
            {
                oldRank1NewScore = calcAssimilatorPayoffTermNew(x) / 75;
            }

            //pick item between oldRank1NewScore and secondBestOptionNewScore
            if (oldRank1NewScore >= secondBestOptionNewScore)
            {
                //use old best
                //name doesn't change
            }
            else
            {
                //use new best
                name = newOptionName;
            }

            return name;
        }
        private string findBestEconOption(double[] scores)
        {
            int maxSpot = 0;
            double maxScore = scores[0];
            for (int i = 1; i < 4; i++)
            {
                if (scores[i] > maxScore)
                {
                    maxScore = scores[i];
                    maxSpot = i;
                }
            }
            List<string> newList = new List<string> { "Probe", "assimilator", "nexus", "pylon" };
            return newList[maxSpot];
        }
        private double[,] getNNFeaturesEconOnly(int x)
        {
            string econString = getEconFeatures(x);
            //parse at commas.
            List<double> econList = econString.Split(',').Select(double.Parse).ToList();
            double[,] toReturn = new double[1, econList.Count()];
            for (int i = 0; i < econList.Count(); i++)
            {
                toReturn[0, i] = econList[i];
            }
            return toReturn;
        }
        private double[,] getNNFeaturesEventVsEcon(int x)
        {
            //get extrapolated features
            simulatedSlimBuild = new List<ProcessFriendlyBuildObject>(SlimBuild);
            double e0 = simulatedSlimBuild[simulatedSlimBuild.Count - 1].minTime + timeDelayed + lastUnitDuration - currentTime;
            double e1 = simulatedSlimBuild[x].minTime + timeDelayed - currentTime;
            double e2 = simulatedSlimBuild[x].mineralCost;
            double e3 = simulatedSlimBuild[x].gasCost;
            double e4 = simulatedSlimBuild[x].supplyCost;
            double p0 = calcPylonBottleneckDifferentialTerm(x);
            //also calculate pylon bottleneck term if no probe production is assumed
            double p1 = calcPylonBottleneckDifferentialTerm(x, true); //1 for no probes
                                                                      //from nexus pull payoff
            double[] nexusTerms = calcNexusPayoffTermsToLog(x, e0);
            double n0 = nexusTerms[0];
            double n1 = nexusTerms[1];
            double n2 = nexusTerms[2];
            double n3 = nexusTerms[3];
            double n4 = nexusTerms[4];
            double n5 = nexusTerms[5];
            double n6 = nexusTerms[6];
            double n7 = nexusTerms[7];

            //from assimilator 
            //if already have max num assimilators (check # of nexi done in next 24 seconds)
            int numExtraNexi = 0;
            foreach (buildOrderEntry thisEntry in unfinishedEventLog)
            {
                if (thisEntry.itemToBuild == "nexus" && thisEntry.timeToBuild < currentTime + 21)
                {
                    numExtraNexi++;
                }
            }
            double a0 = 0;
            double a1 = 0;
            double a2 = 0;
            if ((buildingStatusTracker.nexusCount + numExtraNexi) * 2 > buildingStatusTracker.futureAssimilatorCount)
            {
                aVals = new double[3];
                aVals[1] = 1;
                aVals[2] = 1;
                a0 = calcAssimilatorDelayDifferential(x, 1);
                a1 = calcAssimilatorDelayDifferential(x, 5);
                a2 = calcAssimilatorDelayDifferential(x, 10);
            }
            double[] probeTerms = calcProbePayoffToLog(x, e0);
            double pr0 = probeTerms[0];
            double pr1 = probeTerms[1];
            double pr2 = probeTerms[2];
            double pr3 = probeTerms[3];
            //add a feature for if next event is WGR
            double WGRFeature = 0;
            if (simulatedSlimBuild[x].name == "warpGateResearch")
            {
                WGRFeature = 1;
            }

            //add 8 state variables
            double[,] features = new double[,] { { mineralBank, gasBank, buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount,
                    buildingStatusTracker.futureAssimilatorCount, buildingStatusTracker.freeNexusCount, x,currentSupply, currentSupplyCap,
                    e0,e1,e2,e3,e4,p0,p1,n0,n1,n2,n3,n4,n5,n6,n7,a0,a1,a2,pr0,pr1,pr2,pr3,WGRFeature } };
            return features;
        }

        private double calcProbePayoffNew(int spotInBuild, double eventTermNow)
        {
            double timeAffectMineralConstraint = calcProbePayOffTerm(spotInBuild);
            //find gas and mineral Term now
            int totalMineralsToSpend = 0;
            int totalGasToSpend = 0;
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
                totalGasToSpend = totalGasToSpend + simulatedSlimBuild[x].gasCost;
            }
            //calculate time until afford gas component
            double gasIncome = buildingStatusTracker.futureNexusCount * 2 * 169.61 / 60;
            double gasTermNow = (totalGasToSpend - gasBank) / gasIncome;

            double mineralIncomeNow = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount);
            double mineralIncomeWith1MoreProbe = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount + 1);
            double mineralTermNow = totalMineralsToSpend / mineralIncomeNow;
            double mineralTermThen = (totalMineralsToSpend + 50 + 12) / mineralIncomeWith1MoreProbe;
            //if (probePayOffTerm < 0) { probePayOffTerm = 0; } //min value is 0

            //find event delay if get probe
            double timeUntilFreeNexus;
            if (buildingStatusTracker.freeNexusCount > 0) { timeUntilFreeNexus = 0; }
            else { timeUntilFreeNexus = unfinishedEventLog.First(x => x.itemToBuild.Equals("Probe")).timeToBuild - currentTime; }

            //find when next event will occur, base on largest of time, mineral, and gas constraints
            double timeConstraintNextEvent = simulatedSlimBuild[spotInBuild].minTime + timeDelayed - currentTime;
            double mineralConstraintNextEvent = simulatedSlimBuild[spotInBuild].mineralCost / mineralBank;
            List<double> emptyList = new List<double>();
            int totalGasCost = simulatedSlimBuild[spotInBuild].gasCost;
            double gasConstraintNextEvent = findGasConstraint(totalGasCost, 10000); //assimilator finish time set excessively large since there isn't one being started

            //find max of all 3 constraints
            double timeUntilNextEvent = timeConstraintNextEvent;
            if (mineralConstraintNextEvent > timeUntilNextEvent) { timeUntilNextEvent = mineralConstraintNextEvent; }
            if (gasConstraintNextEvent > timeUntilNextEvent) { timeUntilNextEvent = gasConstraintNextEvent; }

            //find time waiting around
            double timeWaiting = timeUntilFreeNexus - timeUntilNextEvent;
            if (timeWaiting < 0) { timeWaiting = 0; }
            //find the amount of money sitting around * the time it was sitting around
            double unspentIntegral = mineralBank * timeWaiting + mineralIncome * timeWaiting / 2 * timeWaiting;
            if (unspentIntegral < 0 || timeWaiting < 0) { unspentIntegral = 0; }



            //determine max of 
            double maxTime = findMaxOfThree(mineralTermThen, gasTermNow, eventTermNow+timeWaiting);
            double mineralTerm = (mineralTermNow - mineralTermThen) * 20 / (20 + maxTime - mineralTermThen);
            double netTerm = mineralTerm * 20 / (20 + unspentIntegral);
            return netTerm;
        }
		private double[] calcProbePayoffToLog(int spotInBuild, double eventTermNow)
		{
			double[] termsToReturn = new double[4];
			double timeAffectMineralConstraint = calcProbePayOffTerm(spotInBuild);
			termsToReturn[0] = timeAffectMineralConstraint;
			//find gas and mineral Term now
			int totalMineralsToSpend = 0;
			int totalGasToSpend = 0;
			for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
			{
				totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
				totalGasToSpend = totalGasToSpend + simulatedSlimBuild[x].gasCost;
			}
			//calculate time until afford gas component
			double gasIncome = buildingStatusTracker.futureNexusCount * 2 * 169.61 / 60;
			double gasTermNow = (totalGasToSpend - gasBank) / gasIncome;

			double mineralIncomeNow = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount);
			double mineralIncomeWith1MoreProbe = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount + 1);
			double mineralTermNow = totalMineralsToSpend / mineralIncomeNow;
			double mineralTermThen = (totalMineralsToSpend + 50 + 12) / mineralIncomeWith1MoreProbe;
			//if (probePayOffTerm < 0) { probePayOffTerm = 0; } //min value is 0
			
			//find event delay if get probe
			double timeUntilFreeNexus;
			if (buildingStatusTracker.freeNexusCount > 0) { timeUntilFreeNexus = 0; }
			else { timeUntilFreeNexus = unfinishedEventLog.First(x => x.itemToBuild.Equals("Probe")).timeToBuild - currentTime; }

			//find when next event will occur, base on largest of time, mineral, and gas constraints
			double timeConstraintNextEvent = simulatedSlimBuild[spotInBuild].minTime + timeDelayed - currentTime;
			double mineralConstraintNextEvent = simulatedSlimBuild[spotInBuild].mineralCost / mineralBank;
			List<double> emptyList = new List<double>();
			int totalGasCost = simulatedSlimBuild[spotInBuild].gasCost;
			double gasConstraintNextEvent = findGasConstraint(totalGasCost, 10000); //assimilator finish time set excessively large since there isn't one being started

			//find max of all 3 constraints
			double timeUntilNextEvent = timeConstraintNextEvent;
			if (mineralConstraintNextEvent > timeUntilNextEvent) { timeUntilNextEvent = mineralConstraintNextEvent; }
			if (gasConstraintNextEvent > timeUntilNextEvent) { timeUntilNextEvent = gasConstraintNextEvent; }

			//find time waiting around
			double timeWaiting = timeUntilFreeNexus - timeUntilNextEvent;
			if (timeWaiting < 0) { timeWaiting = 0; }
			//find the amount of money sitting around * the time it was sitting around
			double unspentIntegral = mineralBank * timeWaiting + mineralIncome * timeWaiting / 2 * timeWaiting;
			if (unspentIntegral < 0 || timeWaiting < 0) { unspentIntegral = 0; }



			//determine max of 
			double maxTime = findMaxOfThree(mineralTermThen, gasTermNow, eventTermNow + timeWaiting);
			double mineralTerm = (mineralTermNow - mineralTermThen) * 20 / (20 + maxTime - mineralTermThen);
			double netTerm = mineralTerm * 20 / (20 + unspentIntegral);
			termsToReturn[1] = mineralTerm;
			termsToReturn[2] = unspentIntegral;
			termsToReturn[3] = netTerm;
			return termsToReturn;
		}
		private double calcAssimilatorPayoffTermNew(int x)
        {
			//return 0 if already have max num assimilators (check # of nexi done in next 24 seconds)
			int numExtraNexi = 0;
			foreach (buildOrderEntry thisEntry in unfinishedEventLog)
			{
				if (thisEntry.itemToBuild == "nexus" && thisEntry.timeToBuild < currentTime + 21)
				{
					numExtraNexi++;
				}
			}
			if ((buildingStatusTracker.nexusCount+numExtraNexi) * 2 <= buildingStatusTracker.futureAssimilatorCount) { return 0; }
            double a1 = calcAssimilatorDelayDifferential(x, 1);                                                                                                      
            double a2 = calcAssimilatorDelayDifferential(x, 5);                   
            double a3 = calcAssimilatorDelayDifferential(x, 10);
            double eventDelayTerm = 0.5 * a1 + 0.3 * a2 + 0.2 * a3;

			
            return eventDelayTerm;
        }
        private string findBestName(double[] scoring,string eventName)
        {
            //results -> probe,assim,event,nexus,pylon
            double bestScore = scoring.Max();
            int bestSpot = scoring.ToList().IndexOf(bestScore);
            if (bestSpot == 0) { return "Probe"; }
            else if (bestSpot == 1) { return "assimilator"; }
            else if (bestSpot == 2) { return eventName; }
            else if (bestSpot == 3) { return "nexus"; }
            else { return "pylon"; }
        }
        public double calculateProbeOverallValue(int spotInBuild)
        {
            //probe rates
            double probeGen = Math.Pow(calcProbeGeneralTerm(spotInBuild), prVals[1]);                                      // mineral constraint over total build 
            double probeDifferential = Math.Pow(calcProbeDifferentialTerm(spotInBuild), prVals[2]);                        // income increase from another probe
            double probePayOff = Math.Pow(calcProbePayOffTerm(spotInBuild), prVals[4]);                                    // how +1 probe impacts mineral constaint on build [pr3(12), accounts for added probe not mining while building]
            double probeQueueBlock = Math.Pow(calcProbeQueueBlockTerm(spotInBuild), prVals[5]);                            // (R) 1/(amount of money held up * time held up) due to waiting on queue
            double valueProbe = prVals[0] * probeGen * probeDifferential * probePayOff * probeQueueBlock;
            return valueProbe;
        }
        public double calculatePylonOverallValue(int spotInBuild)
        {
            //pylon rates
            double pylonBottleneck = calcPylonBottleneckDifferentialTerm(spotInBuild);                                    // (R) bottleneck difference between now and next 
            double valuePylon = pyVals[0] * pylonBottleneck;
            return valuePylon;
        }
        public double calculateNexusOverallValue(int spotInBuild)
        {
            //nexus rates
            double nexusGen = Math.Pow(calcNexusGeneralTerm(spotInBuild), nVals[1]);                                       //mineral constraint over total build 
            double nexusPayOff = Math.Pow(calcNexusPayoffTerm(spotInBuild), nVals[3]);                                    // how much faster will build be done with 1 more full sat nexus [n2(400) added cost spent to saturate new nexus]
            double nexusDifferential = Math.Pow(calcNexusIncomeDifferentialTerm(spotInBuild), nVals[6]);                   //income difference with probes split amongst another nexus [n4(10) probes done when future nexus done,n5(10)probes done when future nexus would be done]
            double nexusBottleneck = Math.Pow(calcNexusBottleneckTerm(spotInBuild), nVals[7]);                             // (R) 1/how much will getting nexus slow down build
            double entrySpot = Math.Pow(calcNexusEntrySpotTerm(spotInBuild), nVals[10]);                                   //how does buying now compare to buying in next minute [n8(10) end of first interval,n9(60)end of second interval]
            double valueNexus = nVals[0] * nexusGen * nexusPayOff * nexusDifferential * nexusBottleneck * entrySpot;
            return valueNexus;
        }
        public double calculateAssimilatorOverallValue(int spotInBuild)
        {
            //assimilator rates
            double assimilatorGen = Math.Pow(calcAssimilatorGenTerm(spotInBuild), aVals[1]);                               // gas constraint over total build
            double assimilatorNumConstraint = Math.Pow(calcAssimilatorNumConstraint(spotInBuild), aVals[3]);               // how many open vesphene geysers [a2(0)-time +21 seconds before nexus done when can build assimilator]
                                                                                                                           //calculate DelayTerms;
            double shorttermDelayTerm = calcAssimilatorDelayDifferential(spotInBuild, 1);                             // (R) how will delaying assimilator impact next unit
            double midtermDelayTerm = aVals[4] * calcAssimilatorDelayDifferential(spotInBuild, 5);                     // (R) how will delaying assimilator impact next 5 units 
            double longtermDelayTerm = aVals[5] * calcAssimilatorDelayDifferential(spotInBuild, 10);                   // (R) how will delaying assimilator impact next 10 units 
            double overallDelayTerm = Math.Pow(shorttermDelayTerm + midtermDelayTerm + longtermDelayTerm, aVals[6]); //weighted sum 
            if (overallDelayTerm < 0) { overallDelayTerm = 0; }
            double valueAssimilator = aVals[0] * assimilatorGen * assimilatorNumConstraint * overallDelayTerm;
            return valueAssimilator;
        }

        public string findNameOfXBest(double[] options, int x)
        {
            string name;
            double score = options.OrderByDescending(i => i).ToList()[x - 1];
            if (score == options[0])
            {
                name = "pylon";
            }
            else if (score == options[1])
            {
                name = "nexus";
            }
            else if (score == options[2])
            {
                name = "Probe";
            }
            else if (score == options[3])
            {
                name = "assimilator";
            }
            else
            {
                name = "should never reach this line";
            }


            return name;
        }
        public void updateSimulatedSlimBuildWithEcoOption(double[] options, double highestScore, int x)
        {
            if (highestScore == options[0])
            {
                ProcessFriendlyBuildObject simulatedBuildEntry = new ProcessFriendlyBuildObject(100, 0, 0, 8, "pylon", 0);
                simulatedSlimBuild.Insert(x, simulatedBuildEntry);
            }
            else if (highestScore == options[1])
            {
                ProcessFriendlyBuildObject simulatedBuildEntry = new ProcessFriendlyBuildObject(400, 0, 0, 15, "nexus", 0);
                simulatedSlimBuild.Insert(x, simulatedBuildEntry);
            }
            else if (highestScore == options[2])
            {
                //find soonest probe start time
                double probeMinBuildTime = currentTime - timeDelayed;
                if (buildingStatusTracker.freeNexusCount == 0)
                {
                    //find when a nexus is free
                    List<buildOrderEntry> orderedUnfinishedEvents = unfinishedEventLog.OrderBy(i => i.timeToBuild).ToList();
                    foreach (buildOrderEntry thisEntry in orderedUnfinishedEvents)
                    {
                        if (thisEntry.itemToBuild.Equals("Probe"))
                        {
                            probeMinBuildTime = thisEntry.timeToBuild - timeDelayed;
                            break;
                        }
                    }
                }
                ProcessFriendlyBuildObject simulatedBuildEntry = new ProcessFriendlyBuildObject(50, 0, 1, 0, "Probe", probeMinBuildTime);
                simulatedSlimBuild.Insert(x, simulatedBuildEntry);
            }
            else if (highestScore == options[3])
            {
                ProcessFriendlyBuildObject simulatedBuildEntry = new ProcessFriendlyBuildObject(75, 0, 0, 0, "assimilator", 0);
                simulatedSlimBuild.Insert(x, simulatedBuildEntry);
            }
            else
            {
                string name = "should never reach this line";
            }
        }
        public double calcPylonBottleneckDifferentialTerm(int spotInBuild, bool noProbes=false)
        {
            double bottleneckNext;
            double bottleneckNow;
            if (currentSupply == 13 && realizedBuildLog.Count() == 1) { return 0; }

            //if we build a pylon now how long will we be supply blocked for?
            //find pylon start time
            double nowPylonStartTime;
            //mining does not occur for first 4 seconds, so adjusting calculation
            if (currentTime < 4)
            {
                double thisMineralIncome = calculateMineralIncome(1, 12);
                nowPylonStartTime = (100 - mineralBank) / thisMineralIncome + 4 + currentTime; //pylon costs 100 minerals
            }
            else
            {
                nowPylonStartTime = (100 - mineralBank) / mineralIncome + currentTime; //pylon costs 100 minerals
            }
            if (nowPylonStartTime < currentTime) { nowPylonStartTime = currentTime; }
            double nowPylonEndTime = nowPylonStartTime + 18; //pylon takes 18 seconds to build
            //consider how supply will rise from now to pylonEndTime
            bottleneckNow = predictSupplyBlock(nowPylonEndTime, false, spotInBuild, nowPylonStartTime - currentTime,noProbes);


            //if we perform the next event and then build a pylon, how long will we be supply blocked for?
            //consider new start time of pylon, must occur after next event
            double timeUntilNextEvent = calculateExpectedTimeUntilNextEvent(spotInBuild);

            //calculate how many minerals will have after next event is purchased
            //account for mining not taking place for first 4 seconds
            double futureMineralBank;
            if (currentTime < 4 && timeUntilNextEvent + currentTime > 4) //if next event is not in first 4 seconds
            {
                //only consider income made after first 4 seconds
                double timeMining = timeUntilNextEvent + currentTime - 4;
                double miningRate = calculateMineralIncome(1, 12);
                futureMineralBank = mineralBank + timeMining * miningRate - simulatedSlimBuild[spotInBuild].mineralCost;
            }
            else
            {
                futureMineralBank = mineralBank + timeUntilNextEvent * mineralIncome - simulatedSlimBuild[spotInBuild].mineralCost;
            }
            double laterPylonStartTime;
            if (futureMineralBank >= 100) { laterPylonStartTime = timeUntilNextEvent + currentTime; }
            else
            {
                //need to find how long it will take to mine 100-futureMineralBank
                //if current time is under 4 seconds, account for change in income at 4 seconds
                if (currentTime < 4)
                {
                    //if nextEvent is before 4 seconds, then need to add the time of (4-timeOfNextEvent)
                    double extraTime = 4 - (timeUntilNextEvent + currentTime);
                    if (extraTime < 0) { extraTime = 0; }
                    double futureMiningRate = calculateMineralIncome(1, 12);
                    laterPylonStartTime = (100 - futureMineralBank) / futureMiningRate + timeUntilNextEvent + currentTime + extraTime;
                }
                else
                {
                    laterPylonStartTime = (100 - futureMineralBank) / mineralIncome + timeUntilNextEvent + currentTime;
                }
            }
            double laterPylonEndTime = laterPylonStartTime + 18; //pylon takes 18 seconds to build
            //consider how for 18 seconds after that supply will rise
            bottleneckNext = predictSupplyBlock(laterPylonEndTime, true, spotInBuild, timeUntilNextEvent, noProbes);
            double bottleneckDifferentialTerm = bottleneckNext - bottleneckNow;

            return bottleneckDifferentialTerm;
        }

        public double predictSupplyBlock(double pylonEndTime, bool performNextEventBeforePylon, int spotInBuild, double timeUntilNextEvent, bool noProbes)
        {
            //goal is to find when gets supply blocked or supply freed in interval t=currentTime to t=endTime
            //use simplifeid version for now. assume events takes place at minTime+timeDelayed
            //supply will increase when eventOccurs or probe is built.
            //supply cap will increase when an unfinished event 

            //make a list of every event in this interval, each item in list has supply cost and supply provided
            List<SupplyBlockTracker> eventList = addEventsToSupplyBlockTracker(spotInBuild, pylonEndTime);

            //add unfinished events 
            eventList = addUnfinishedEventsToSupplyBlockTracker(eventList, spotInBuild, pylonEndTime);

            //add events that represent constant probe production
            if (!noProbes)
            {
                eventList = addTheoreticalProbesToSupplyBlockTracker(eventList, timeUntilNextEvent, pylonEndTime);
            }
            

            //List<SupplyBlockTracker> orderedEventList = new List<SupplyBlockTracker>(eventList.OrderBy(x => x.timeOccur));
            compareSupplyBlockTracker c = new compareSupplyBlockTracker();
            eventList.Sort(c);
            //simulation variables
            int simSupplyCap = currentSupplyCap;
            int simSupply = currentSupply;
            double simCurrentTime = currentTime;
            int freeSupply = simSupplyCap - simSupply;
            double timeEnterBlock = 0;
            bool blocked = false;
            double totalTimeBlocked = 0;

            //track through times and note when supply blocks occur
            foreach (SupplyBlockTracker thisEvent in eventList)
            {
                //update supplies
                simSupply = simSupply + thisEvent.supplyCost;
                simSupplyCap = simSupplyCap + thisEvent.supplyProvided;
                //update time
                simCurrentTime = thisEvent.timeOccur;

                if (blocked) //enter here is was previously blocked
                {
                    if (simSupply <= simSupplyCap)  //check if now not blocked
                    {
                        totalTimeBlocked = simCurrentTime - timeEnterBlock + totalTimeBlocked;
                        blocked = false;
                    }
                }
                else //enter here if wasn't blocked at last event
                {
                    if (simSupply > simSupplyCap) //check if now blocked
                    {
                        //timeEnterBlock = simCurrentTime - .1;
                        timeEnterBlock = simCurrentTime;
                        blocked = true;
                    }
                }
            }
            if (blocked) { totalTimeBlocked = totalTimeBlocked + pylonEndTime - timeEnterBlock; }
            return totalTimeBlocked;
        }
        private List<SupplyBlockTracker> addTheoreticalProbesToSupplyBlockTracker(List<SupplyBlockTracker> eventList, double timeUntilNextEvent, double pylonEndTime)
        {
            List<double> probeTimes = new List<double>();
            foreach (buildOrderEntry thisEntry in unfinishedEventLog)
            {
                if (thisEntry.itemToBuild.Equals("Probe") || thisEntry.itemToBuild.Equals("nexus"))
                {
                    //add end time to list
                    probeTimes.Add(thisEntry.timeToBuild);
                }
            }
            //adjust probeTimes list to account for when probe production can start (after timeUntilNextEvent
            for (int g = probeTimes.Count() - 1; g >= 0; g--)
            {
                if (probeTimes[g] - currentTime < timeUntilNextEvent)
                {
                    probeTimes.RemoveAt(g);
                }
            }


            //find how many nexi didn't have probes queued.
            int simFreeNexi;
            if (probeTimes.Any())
            {
                simFreeNexi = buildingStatusTracker.futureNexusCount - probeTimes.Count();
            }
            else { simFreeNexi = buildingStatusTracker.futureNexusCount; }
            //first add times over next 5 probe cycles then cut down list to only valid times
            int numCycles = (int)Math.Ceiling((pylonEndTime - currentTime - timeUntilNextEvent) / 12);
            if (numCycles < 0) { numCycles = 0; }
            List<SupplyBlockTracker> probeList = new List<SupplyBlockTracker>();
            for (int x = 0; x < numCycles; x++)
            {
                for (int y = 0; y < simFreeNexi; y++)
                {
                    SupplyBlockTracker newProbe = new SupplyBlockTracker(1, 0, currentTime + timeUntilNextEvent + 12 * x);
                    probeList.Add(newProbe);
                }
                for (int z = 0; z < probeTimes.Count(); z++)
                {
                    SupplyBlockTracker newProbe = new SupplyBlockTracker(1, 0, probeTimes[z] + 12 * x);
                    probeList.Add(newProbe);
                }
            }
            //cut down probe list to only needed times
            for (int x = probeList.Count - 1; x >= 0; x--)
            {
                if (probeList[x].timeOccur > pylonEndTime)
                {
                    //remove from list
                    probeList.RemoveAt(x);
                }
            }
            //add probeList to eventList
            eventList.AddRange(probeList);
            return eventList;
        }
        public List<SupplyBlockTracker> addUnfinishedEventsToSupplyBlockTracker(List<SupplyBlockTracker> eventList, int spotInBuild, double pylonEndTime)
        {
            //add events from unfinishedEventLog
            foreach (buildOrderEntry thisEntry in unfinishedEventLog)
            {
                if (thisEntry.timeToBuild < pylonEndTime)
                {
                    if (thisEntry.itemToBuild.Equals("pylon"))
                    {
                        SupplyBlockTracker newEvent = new SupplyBlockTracker(0, 8, thisEntry.timeToBuild);
                        eventList.Add(newEvent);
                    }
                    if (thisEntry.itemToBuild.Equals("nexus"))
                    {
                        SupplyBlockTracker newEvent = new SupplyBlockTracker(0, 15, thisEntry.timeToBuild);
                        eventList.Add(newEvent);
                    }
                }
            }
            return eventList;
        }
        public List<SupplyBlockTracker> addEventsToSupplyBlockTracker(int spotInBuild, double pylonEndTime)
        {
            List<SupplyBlockTracker> eventList = new List<SupplyBlockTracker>();
            for (int x = spotInBuild; x < simulatedSlimBuild.Count; x++)
            {
                if (simulatedSlimBuild[x].minTime + timeDelayed < pylonEndTime)
                {
                    if (simulatedSlimBuild[x].supplyCost > 0 && !simulatedSlimBuild[x].name.Equals("Probe"))
                    {
                        //add to list
                        SupplyBlockTracker newEvent = new SupplyBlockTracker(simulatedSlimBuild[x].supplyCost, 0, simulatedSlimBuild[x].minTime + timeDelayed);
                        eventList.Add(newEvent);
                    }

                }
                else { break; }
            }
            return eventList;
        }

        public double calcProbeGeneralTerm(int spotInBuild)
        {
            //find total minerals left to spend
            int totalMineralsToSpend = 0;
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
            }
            //if mineral Income is effectlively zero, assume it is 12
            double tempMineralIncome = mineralIncome;
            if (mineralIncome < 1) { tempMineralIncome = 12; }
            double generalTerm = totalMineralsToSpend / tempMineralIncome;
            return generalTerm;
        }
        public double calcProbeDifferentialTerm(int spotInBuild)
        {

            double mineralIncomeNow = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount);
            double mineralIncomeWith1MoreProbe = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount + 1);
            double incomeDifferential = mineralIncomeWith1MoreProbe - mineralIncomeNow;
            double incomeDifferentialTerm = incomeDifferential;
            return incomeDifferentialTerm;
        }
        public double calcProbePayOffTerm(int spotInBuild)
        {
            //find total minerals left to spend
            int totalMineralsToSpend = 0;
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
            }
            double mineralIncomeNow = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount);
            double mineralIncomeWith1MoreProbe = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount + 1);
            double probePayOffTerm = totalMineralsToSpend / mineralIncomeNow - (totalMineralsToSpend + 50 + 12) / mineralIncomeWith1MoreProbe;
            if (probePayOffTerm < 0) { probePayOffTerm = 0; } //min value is 0
            return probePayOffTerm;
        }
        public double calcProbeQueueBlockTerm(int spotInBuild)
        {
            double timeUntilFreeNexus;
            if (buildingStatusTracker.freeNexusCount > 0) { timeUntilFreeNexus = 0; }
            else { timeUntilFreeNexus = unfinishedEventLog.First(x => x.itemToBuild.Equals("Probe")).timeToBuild - currentTime; }

            //find when next event will occur, base on largest of time, mineral, and gas constraints
            double timeConstraintNextEvent = simulatedSlimBuild[spotInBuild].minTime + timeDelayed - currentTime;
            double mineralConstraintNextEvent = simulatedSlimBuild[spotInBuild].mineralCost / mineralBank;
            List<double> emptyList = new List<double>();
            int totalGasCost = simulatedSlimBuild[spotInBuild].gasCost;
            double gasConstraintNextEvent = findGasConstraint(totalGasCost, 10000); //assimilator finish time set excessively large since there isn't one being started

            //find max of all 3 constraints
            double timeUntilNextEvent = timeConstraintNextEvent;
            if (mineralConstraintNextEvent > timeUntilNextEvent) { timeUntilNextEvent = mineralConstraintNextEvent; }
            if (gasConstraintNextEvent > timeUntilNextEvent) { timeUntilNextEvent = gasConstraintNextEvent; }

            //find time waiting around
            double timeWaiting = timeUntilFreeNexus - timeUntilNextEvent;

            //find the amount of money sitting around * the time it was sitting around
            double unspentIntegral = mineralBank * timeWaiting + mineralIncome * timeWaiting / 2 * timeWaiting;
            if (unspentIntegral < 1 || timeWaiting < 0) { unspentIntegral = 1; }


            double queueBlockTerm = 1 / unspentIntegral;
            return queueBlockTerm;
        }

        public double calcNexusPayoffTerm(int spotInBuild) //uses nVals[2]
        {
            //find longTermValueTerm
            int totalMineralsToSpend = 0;
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
            }
            //calculate cost in probes to saturate nexi
            double mineralsToSpendOnProbes = (buildingStatusTracker.futureNexusCount * nVals[2] - buildingStatusTracker.futureProbeCount) * 50; //50 minerals per probe
            double mineralsPerNexus = 400; //400 minerals per nexus
            double longTermValueTerm = totalMineralsToSpend / calculateMineralIncome(buildingStatusTracker.futureNexusCount, 24 * buildingStatusTracker.futureNexusCount) - (totalMineralsToSpend + mineralsPerNexus + mineralsToSpendOnProbes) / calculateMineralIncome(buildingStatusTracker.futureNexusCount + 1, 24 * (buildingStatusTracker.futureNexusCount + 1));
            if (longTermValueTerm < 0) { longTermValueTerm = 0; }
            return longTermValueTerm;
        }
        public double calcNexusPayoffTermNew(int spotInBuild, double eventTermNow) //uses nVals[2]
        {
			//m1: 4p, 6s
			//m2: 4p, 12s
			//m3: 16p, 6s
			//m4: 16p, 12s
			//g1: 0.2 currentGI
			//g2: 0.8 currentGI

			//pass in mpW, msW, gW and take average across values above

            //find mineral and gas terms
            int totalMineralsToSpend = 0;
            int totalGasToSpend = 0;
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
                totalGasToSpend = totalGasToSpend + simulatedSlimBuild[x].gasCost;
            }
            //calculate time until afford gas component
            double gasIncomeMaxNow = buildingStatusTracker.futureNexusCount * 2 * 169.61 / 60;
			double gasIncomeMinNow = buildingStatusTracker.assimilatorCount * 169.61 / 60;
			double gasIncomeMaxCalc = gasIncomeMaxNow;
			double gasIncomeMinCalc = gasIncomeMaxNow * 0.2 + gasIncomeMinNow * 0.8;
			double gasIncome = gasIncomeMinCalc * (nVals[3]-1) + gasIncomeMaxCalc * nVals[3];
			double gasTermNow = (totalGasToSpend-gasBank) / gasIncome;
            double gasIncomeThen = (buildingStatusTracker.futureNexusCount+1) * 2 * 169.61 / 60;
            double gasTermThen;
            //assume gas wont be mined for 80 more seconds
            if (gasTermNow <= 80) { gasTermThen = gasTermNow; }
            else
            {
                gasTermThen = (totalGasToSpend - gasBank - 80 * gasIncome) / gasIncomeThen + 80;
            }

			//calculate time until afford mineral component
			//first consider mineral time constaint if saturate current amount of nexi
			double numProbesToSaturate = buildingStatusTracker.futureNexusCount * 16 + buildingStatusTracker.futureAssimilatorCount * 3 - buildingStatusTracker.futureProbeCount;
			if (numProbesToSaturate < 0) { numProbesToSaturate = 0;  }
			double mineralsToSpendOnProbes = numProbesToSaturate * 50; //50 minerals per probe
																	   //assume probes will be done after numProbesToSaturate/6/futureNexusCount seconds
			double timeProbesDone = numProbesToSaturate * 6 / (double)buildingStatusTracker.futureNexusCount;
			double mineralIncomeNow = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount- buildingStatusTracker.futureAssimilatorCount * 3);
			double satMineralIncome = calculateMineralIncome(buildingStatusTracker.futureNexusCount, 16 * buildingStatusTracker.futureNexusCount);
			double mineralTermNowBare = totalMineralsToSpend / mineralIncomeNow;
			if (numProbesToSaturate <= 0) { satMineralIncome = mineralIncomeNow; }
				//adjust for new probes
				//check if already saturated
				double mineralTermNow = (totalMineralsToSpend + mineralsToSpendOnProbes - timeProbesDone * calculateMineralIncome(buildingStatusTracker.futureNexusCount, 16 * buildingStatusTracker.futureNexusCount)) / satMineralIncome + timeProbesDone;
			double mineralTermThenM1 = mineralTermNow;
			double mineralTermThenM2 = mineralTermNow;
			double mineralTermThenM3 = mineralTermNow;
			double mineralTermThenM4 = mineralTermNow;

			if (mineralTermNowBare >= mineralTermNow) //else even saturating existing nexi is bad idea
			{
				mineralTermThenM1 = findNexusMineralTerm(totalMineralsToSpend, mineralIncomeNow, 6, 4);
				mineralTermThenM2 = findNexusMineralTerm(totalMineralsToSpend, mineralIncomeNow, 12, 4);
				mineralTermThenM3 = findNexusMineralTerm(totalMineralsToSpend, mineralIncomeNow, 6, 16);
				mineralTermThenM4 = findNexusMineralTerm(totalMineralsToSpend, mineralIncomeNow, 12, 16);
			}
			//select weighting of mineral Terms using coefficients
			//do simplified version of double interpolation. S axis, P axis
			//Given MS and MP
			//find weighted average between MS at p=16 and MS at P=4
			double MS = nVals[1];
			double MP = nVals[2];
			double p4SAv = mineralTermThenM1 * MS + mineralTermThenM2 * (1 - MS);
			double p16SAv = mineralTermThenM3 * MS + mineralTermThenM4 * (1 - MS);
			double netAvPFirst = p4SAv * MP + p16SAv * (1 - MP);
			double s6PAv = mineralTermThenM1 * MP + mineralTermThenM3 * (1 - MP);
			double s12PAv = mineralTermThenM2 * MP + mineralTermThenM4 * (1 - MP);
			double netAVSFirst = s6PAv * MS + s12PAv * (1 - MS);
			double mineralTermThen = (netAvPFirst + netAVSFirst) / 2;

			//find event delay
			//find bottleneckterm
			double expectedTimeUntilNextEvent = calculateExpectedTimeUntilNextEvent(spotInBuild);
            //find new time to afford if 400 minerals more expensive
            double timeUntilAffordWithNexus = (simulatedSlimBuild[spotInBuild].mineralCost + 400 - mineralBank) / Math.Max(mineralIncome,12);
            double bottleNeckValue = 0;
            if (timeUntilAffordWithNexus > expectedTimeUntilNextEvent) { bottleNeckValue = timeUntilAffordWithNexus - expectedTimeUntilNextEvent; }
            double eventTermThen = eventTermNow + bottleNeckValue;
            //now have eventTermNow, eventTermThen,gasTermNow, gasTermThen, mineralTermNow,mineralTermThen
            //find maximum of times
            double maxTimeNow = findMaxOfThree(eventTermNow, mineralTermNow, gasTermNow);
            double maxTimeThen = findMaxOfThree(eventTermThen, mineralTermThen, gasTermThen);
            //compute value
            double mineralTerm = (mineralTermNow - mineralTermThen) * 20 / (20 + (maxTimeThen - mineralTermThen));
            double gasTerm = (gasTermNow - gasTermThen) * 5 / (5 + (maxTimeThen - gasTermThen)); //gas term falls off harder because only relevant if build is very gas starved
            double eventTerm = nVals[4] * (eventTermNow - eventTermThen) * 20 / (20 + (maxTimeThen - eventTermThen));
            double netTerm = mineralTerm + gasTerm + eventTerm;
            if (netTerm < 0) { netTerm = 0; }
            return netTerm;
        }
		public double[] calcNexusPayoffTermsToLog(int spotInBuild, double eventTermNow) //uses nVals[2]
		{
			//m1: 4p, 6s
			//m2: 4p, 12s
			//m3: 16p, 6s
			//m4: 16p, 12s
			//g1: 0.2 currentGI
			//g2: 0.8 currentGI
			double[] termsToReturn = new double[8];
			//pass in mpW, msW, gW and take average across values above

			//find mineral and gas terms
			int totalMineralsToSpend = 0;
			int totalGasToSpend = 0;
			for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
			{
				totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
				totalGasToSpend = totalGasToSpend + simulatedSlimBuild[x].gasCost;
			}
			//calculate time until afford gas component
			double gasIncomeMaxNow = buildingStatusTracker.futureNexusCount * 2 * 169.61 / 60;
			double gasIncomeMinNow = buildingStatusTracker.assimilatorCount * 169.61 / 60;
			double gasIncomeMaxCalc = gasIncomeMaxNow;
			double gasIncomeMinCalc = gasIncomeMaxNow * 0.2 + gasIncomeMinNow * 0.8;
			double gasIncome = gasIncomeMinCalc * 0 + gasIncomeMaxCalc * 1;
			double gasTermNow = (totalGasToSpend - gasBank) / gasIncome;
			double gasIncomeThen = (buildingStatusTracker.futureNexusCount + 1) * 2 * 169.61 / 60;
			double gasTermThen;
			//assume gas wont be mined for 80 more seconds
			if (gasTermNow <= 80) { gasTermThen = gasTermNow; }
			else
			{
				gasTermThen = (totalGasToSpend - gasBank - 80 * gasIncome) / gasIncomeThen + 80;
			}

			//calculate time until afford mineral component
			//first consider mineral time constaint if saturate current amount of nexi
			double numProbesToSaturate = buildingStatusTracker.futureNexusCount * 16 + buildingStatusTracker.futureAssimilatorCount * 3 - buildingStatusTracker.futureProbeCount;
			double mineralsToSpendOnProbes = numProbesToSaturate * 50; //50 minerals per probe
																	   //assume probes will be done after numProbesToSaturate/6/futureNexusCount seconds
			double timeProbesDone = numProbesToSaturate * 6 / (double)buildingStatusTracker.futureNexusCount;
			double mineralIncomeNow = calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount - buildingStatusTracker.futureAssimilatorCount * 3);
			double satMineralIncome = calculateMineralIncome(buildingStatusTracker.futureNexusCount, 16 * buildingStatusTracker.futureNexusCount);
			double mineralTermNowBare = totalMineralsToSpend / mineralIncomeNow;
			//adjust for new probes
			//check if already saturated

			double mineralTermNow = (totalMineralsToSpend + mineralsToSpendOnProbes - timeProbesDone * calculateMineralIncome(buildingStatusTracker.futureNexusCount, 16 * buildingStatusTracker.futureNexusCount)) / satMineralIncome + timeProbesDone;
			double mineralTermThenM1 = mineralTermNow;
			double mineralTermThenM2 = mineralTermNow;
			double mineralTermThenM3 = mineralTermNow;
			double mineralTermThenM4 = mineralTermNow;

			if (mineralTermNowBare > mineralTermNow) //else even saturating existing nexi is bad idea
			{
				mineralTermThenM1 = findNexusMineralTerm(totalMineralsToSpend, mineralIncomeNow, 6, 4);
				mineralTermThenM2 = findNexusMineralTerm(totalMineralsToSpend, mineralIncomeNow, 12, 4);
				mineralTermThenM3 = findNexusMineralTerm(totalMineralsToSpend, mineralIncomeNow, 6, 16);
				mineralTermThenM4 = findNexusMineralTerm(totalMineralsToSpend, mineralIncomeNow, 12, 16);
			}
			termsToReturn[0] = mineralTermThenM1- mineralTermNow;
			termsToReturn[1] = mineralTermThenM2- mineralTermNow;
			termsToReturn[2] = mineralTermThenM3- mineralTermNow;
			termsToReturn[3] = mineralTermThenM4- mineralTermNow;
            //select weighting of mineral Terms using coefficients
            //do simplified version of double interpolation. S axis, P axis
            //Given MS and MP
            //find weighted average between MS at p=16 and MS at P=4
            double MS = 1;
			double MP = 1;
			double p4SAv = mineralTermThenM1 * MS + mineralTermThenM2 * (1 - MS);
			double p16SAv = mineralTermThenM3 * MS + mineralTermThenM4 * (1 - MS);
			double netAvPFirst = p4SAv * MP + p16SAv * (1 - MP);
			double s6PAv = mineralTermThenM1 * MP + mineralTermThenM3 * (1 - MP);
			double s12PAv = mineralTermThenM2 * MP + mineralTermThenM4 * (1 - MP);
			double netAVSFirst = s6PAv * MS + s12PAv * (1 - MS);
			double mineralTermThen = (netAvPFirst + netAVSFirst) / 2;

			//find event delay
			//find bottleneckterm
			double expectedTimeUntilNextEvent = calculateExpectedTimeUntilNextEvent(spotInBuild);
			//find new time to afford if 400 minerals more expensive
			double timeUntilAffordWithNexus = (simulatedSlimBuild[spotInBuild].mineralCost + 400 - mineralBank) / Math.Max(mineralIncome, 12);
			double bottleNeckValue = 0;
			if (timeUntilAffordWithNexus > expectedTimeUntilNextEvent) { bottleNeckValue = timeUntilAffordWithNexus - expectedTimeUntilNextEvent; }
			double eventTermThen = eventTermNow + bottleNeckValue;

			//now have eventTermNow, eventTermThen,gasTermNow, gasTermThen, mineralTermNow,mineralTermThen
			//find maximum of times
			double maxTimeNow = findMaxOfThree(eventTermNow, mineralTermNow, gasTermNow);
			double maxTimeThen = findMaxOfThree(eventTermThen, mineralTermThen, gasTermThen);
			//compute value
			double mineralTerm = (mineralTermNow - mineralTermThen) * 20 / (20 + (maxTimeThen - mineralTermThen));
			double gasTerm = (gasTermNow - gasTermThen) * 5 / (5 + (maxTimeThen - gasTermThen)); //gas term falls off harder because only relevant if build is very gas starved
			double eventTerm = 1 * (eventTermNow - eventTermThen) * 20 / (20 + (maxTimeThen - eventTermThen));
			double netTerm = mineralTerm + gasTerm + eventTerm;
			if (netTerm < 0) { netTerm = 0; }
			termsToReturn[4] = mineralTerm;
			termsToReturn[5] = gasTerm;
			termsToReturn[6] = eventTerm;
			termsToReturn[7] = netTerm;
			return termsToReturn;
		}
		private double findNexusMineralTerm(int totalMineralsToSpend, double mineralIncomeNow, int numSec, int numProbes)
		{
			//If buy a nexus (num Probes To Saturate increases, time Probes done increases, 
			double numProbesToSaturate = (buildingStatusTracker.futureNexusCount) * 16 + numProbes + buildingStatusTracker.futureAssimilatorCount * 3 - buildingStatusTracker.futureProbeCount;
			double mineralsToSpendOnProbes = numProbesToSaturate * 50;
			double timeProbesDone = numProbesToSaturate * numSec / (double)buildingStatusTracker.futureNexusCount;
			double satMineralIncome = calculateMineralIncome(buildingStatusTracker.futureNexusCount + 1, 16 * (buildingStatusTracker.futureNexusCount + 1));
			//assume that during timeProbesDone mining at mineralIncomeNow
			double mineralTermThen = (totalMineralsToSpend + 400 + mineralsToSpendOnProbes - timeProbesDone * mineralIncomeNow) / satMineralIncome + timeProbesDone;
			return mineralTermThen;
		}
        private double findMaxOfThree(double a, double b, double c)
        {
            double biggest = a;
            if (b > a) { biggest = b; }
            if (c > biggest) { biggest = c; }
            return biggest;
        }
        public double calcNexusIncomeDifferentialTerm(int spotInBuild)
        {
            int totalMineralsToSpend = 0;
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
            }
            //calculate incomeDifferentialTerm
            double incomeDifferentialTerm = calculateMineralIncome(buildingStatusTracker.futureNexusCount + 1, buildingStatusTracker.futureProbeCount + nVals[4]) - calculateMineralIncome(buildingStatusTracker.futureNexusCount, buildingStatusTracker.futureProbeCount + nVals[5]);

            //make value 1 at minimum. even if income difference isn't apparent yet, may still be right decision if other factors are big enough
            if (incomeDifferentialTerm < 1) { incomeDifferentialTerm = 1; }
            return incomeDifferentialTerm;
        }

        public double calcNexusBottleneckTerm(int spotInBuild)
        {
            //find bottleneckterm
            double expectedTimeUntilNextEvent = calculateExpectedTimeUntilNextEvent(spotInBuild);
            //find new time to afford if 400 minerals more expensive
            double timeUntilAffordWithNexus = (simulatedSlimBuild[spotInBuild].mineralCost + 400 - mineralBank) / mineralIncome;
            double bottleNeckValue;
            if (timeUntilAffordWithNexus > expectedTimeUntilNextEvent)
            {
                bottleNeckValue = timeUntilAffordWithNexus - expectedTimeUntilNextEvent;
            }
            else
            {
                bottleNeckValue = 0;
            }
            double bottleNeckTerm = 1 / (0.001 + bottleNeckValue);
            return bottleNeckTerm;
        }
        public double calcNexusGeneralTerm(int spotInBuild)
        {
            //find generalTerm
            int totalMineralsToSpend = 0;
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                totalMineralsToSpend = totalMineralsToSpend + simulatedSlimBuild[x].mineralCost;
            }
            //if mineral Income is effectlively zero, assume it is 12
            double tempMineralIncome = mineralIncome;
            if (mineralIncome < 1) { tempMineralIncome = 12; }
            double generalTerm = totalMineralsToSpend / tempMineralIncome;
            return generalTerm;
        }
        public double calcNexusEntrySpotTerm(int spotInBuild)
        {
            //find entrySpotTerm
            //find how many minerals expecting to spend in next a16 seconds, 
            //compare to how many minerals expecting to spend in a16 to a17 seconds
            int mineralsToSpendInFirstInterval = 0;
            int mineralsToSpendInSecondInterval = 0;
            for (int i = spotInBuild; i < simulatedSlimBuild.Count(); i++)
            {
                if (simulatedSlimBuild[i].minTime + timeDelayed - currentTime < nVals[8])
                {
                    mineralsToSpendInFirstInterval = mineralsToSpendInFirstInterval + simulatedSlimBuild[i].mineralCost;
                }
                else if (simulatedSlimBuild[i].minTime + timeDelayed - currentTime < nVals[9])
                {
                    mineralsToSpendInSecondInterval = mineralsToSpendInSecondInterval + simulatedSlimBuild[i].mineralCost;
                }
                else { break; }
            }
            if (mineralsToSpendInFirstInterval < 10) { mineralsToSpendInFirstInterval = 10; }
            if (mineralsToSpendInSecondInterval < 10) { mineralsToSpendInSecondInterval = 10; }
            // find how spending rate changes between intervals
            double entrySpotTerm = mineralsToSpendInSecondInterval / mineralsToSpendInFirstInterval * nVals[8] / (nVals[9] - nVals[8]);
            return entrySpotTerm;
        }

        public double calcAssimilatorGenTerm(int spotInBuild)
        {
            //calculate general term
            int totalGasToSpend = 0;
            double tempGasIncome = gasIncome;
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                if (simulatedSlimBuild[x].gasCost > 0)
                {
                    totalGasToSpend = totalGasToSpend + simulatedSlimBuild[x].gasCost;
                }
            }
            if (gasIncome == 0) { tempGasIncome = 1; }
            double generalTerm = totalGasToSpend / tempGasIncome;
            return generalTerm;
        }
        public double calcAssimilatorNumConstraint(int spotInBuild)
        {
            //find number of nexi done in next aVals[2] +21 seconds (assimilator build time is 21 seconds)
            int numNearNexus = 0;
            foreach (buildOrderEntry thisEntry in unfinishedEventLog)
            {
                double timeAway = thisEntry.timeToBuild - currentTime;
                if (thisEntry.timeToBuild - currentTime < 21 && thisEntry.itemToBuild.Equals("nexus"))
                {
                    numNearNexus++;
                }
            }
            double nexusConstraintTerm = ((buildingStatusTracker.nexusCount + numNearNexus) * 2 - buildingStatusTracker.futureAssimilatorCount) / (buildingStatusTracker.nexusCount + numNearNexus);
            return nexusConstraintTerm;
        }
        public double calcAssimilatorDelayDifferential(int spotInBuild, int numGasUnitsForward)
        {
            List<int> spotOfNextGasUnits = findNextGasUnits(spotInBuild);

            //if list is empty return 0
            if (!spotOfNextGasUnits.Any()) { return 0; }
            int spotForward = findGasSpotForward(spotOfNextGasUnits, numGasUnitsForward); //find spot forward to reference

            //calculate cost of gas units up until spotForward

			//multiple this item by aVals[1] (0.67-1.5)
            int totalGasCost = (int)(findTotalGasCostXUnitsForward(spotOfNextGasUnits, numGasUnitsForward) * aVals[1]);

            double timeOfNextGasUnit = findMineralAndBuildConstraint(spotInBuild, spotForward, 0);

            //consider if buy assimilator now
            double timeUntilAssimilatorDone = timeUntilAssimilatorDoneIfBuyASAP();
            double timeAffordGasForGasUnit = findGasConstraint(totalGasCost, timeUntilAssimilatorDone);
            double delayIfBuyNow = timeOfNextGasUnit;
            if (timeAffordGasForGasUnit > timeOfNextGasUnit)
            {
                delayIfBuyNow = timeAffordGasForGasUnit;
            }

            //consider if buy assimilator after event
            double nextEventTime = calculateExpectedTimeUntilNextEvent(spotInBuild);
            //calculate when can start assimilator
            double futureMineralBank = (mineralIncome * nextEventTime - simulatedSlimBuild[spotInBuild].mineralCost + mineralBank);
            double mineralConstraint = (75 - futureMineralBank) / mineralIncome;
            if (mineralConstraint < 0) { mineralConstraint = 0; }
            double futureAssimilatorEndTime = mineralConstraint + nextEventTime + 22; //assimilator build time is 21 seconds (add 1 second for delay in mining)
            timeAffordGasForGasUnit = findGasConstraint(totalGasCost, futureAssimilatorEndTime);
            double delayIfBuyInOne = findMineralAndBuildConstraint(spotInBuild, spotForward, 1);
            if (timeAffordGasForGasUnit > timeOfNextGasUnit)
            {
                delayIfBuyInOne = timeAffordGasForGasUnit;
            }

            double delayTerm = delayIfBuyInOne - delayIfBuyNow;
            return delayTerm;
        }

        public double findGasConstraint(int totalGasCost, double timeUntilAssimilatorDone)
        {
            //consider current gas income, when this assimilator is done, and other building assimilators
            List<double> assimilatorEndTimes = new List<double>();
            assimilatorEndTimes.Add(timeUntilAssimilatorDone);
            foreach (buildOrderEntry thisEntry in unfinishedEventLog)
            {
                if (thisEntry.itemToBuild.Equals("assimilator"))
                {
                    assimilatorEndTimes.Add(thisEntry.timeToBuild - currentTime);
                }
            }

			//add additional end times for each untaken assimilator
			int untakenA = buildingStatusTracker.futureNexusCount * 2 - buildingStatusTracker.futureAssimilatorCount;
			for (int i = 1; i <= untakenA; i++)
			{
				//add assimilator end times (takes 21 seconds to build, consider adding at 10 to 30 seconds after
				double intervalGap = 5 * aVals[2] + 100 * (1 - aVals[2]);
				assimilatorEndTimes.Add(21 + i * intervalGap);
			}

            //find when gasIncome can afford total gas Cost
            //if time is after next double in sortedAssimilatorEndTimes, find gas bank at endTime, and repeat with new income
            double timeAfford = recursiveFindTimeAffordGas(gasIncome, 0, assimilatorEndTimes, totalGasCost, gasBank, 0);

            return timeAfford;
        }

        public double recursiveFindTimeAffordGas(double steppingGasIncome, int stepNum, List<double> sortedAssimilatorEndTimes, int totalGasCost, double steppingGasBank, double steppingCurrentTime)
        {
            //find time afford
            double timeAfford = (totalGasCost - steppingGasBank) / steppingGasIncome + steppingCurrentTime;
            if (timeAfford < 0) { return 0; }

            if (sortedAssimilatorEndTimes.Any()) //first check if anything built ever
            {
                if (sortedAssimilatorEndTimes.Count() > stepNum) //only run if there is another assimilator building
                {
                    if (timeAfford > sortedAssimilatorEndTimes[stepNum])  //check if next assimilator to reference will be done in time
                    {
                        double gasBankAtNextStep = steppingGasIncome * ((sortedAssimilatorEndTimes[stepNum] - steppingCurrentTime));
                        double newSteppingCurrentTime = sortedAssimilatorEndTimes[stepNum];
                        double newSteppingGasIncome = steppingGasIncome + calculateGasIncome(3);
                        timeAfford = recursiveFindTimeAffordGas(newSteppingGasIncome, stepNum + 1, sortedAssimilatorEndTimes, totalGasCost, gasBankAtNextStep, newSteppingCurrentTime);
                    }
                }
            }

            return timeAfford;

        }
        public double findFutureGasBank(double timeOfNextGasUnit, double timeUntilAssimilatorDone)
        {
            double futureGasBank = timeOfNextGasUnit * gasIncome + gasBank + (timeOfNextGasUnit - timeUntilAssimilatorDone) * calculateGasIncome(3);
            //see if any building assimilators would contribute
            foreach (buildOrderEntry thisEntry in unfinishedEventLog)
            {
                if (thisEntry.itemToBuild.Equals("assimilator") && thisEntry.timeToBuild < timeOfNextGasUnit + currentTime)
                {
                    //this item will contribute to future income
                    double timeToContribute = timeOfNextGasUnit + currentTime - thisEntry.timeToBuild;
                    futureGasBank = futureGasBank + timeToContribute * calculateGasIncome(3);
                }
            }
            return futureGasBank;
        }
        public double timeUntilAssimilatorDoneIfBuyASAP()
        {
            double timeToBuildAssimliator = (75 - mineralBank) / mineralIncome;
            if (timeToBuildAssimliator < 0) { timeToBuildAssimliator = 0; }
            //assimilator build time is 21 seconds
            double timeUntilAssimliatorDone = 22 + timeToBuildAssimliator; //21 seconds to build assimilator (+1 second for delay of mining)
            return timeUntilAssimliatorDone;
        }

        public double findMineralAndBuildConstraint(int spotInBuild, int spotForward, int assimilatorPurchaseSpot)
        {
            int assimilatorCost = 0;
            if (spotInBuild + assimilatorPurchaseSpot <= spotForward)
            {
                assimilatorCost = 75;
            }
            int mineralCostUntilGasUnit = assimilatorCost;
            for (int g = spotInBuild; g < spotForward; g++)
            {
                mineralCostUntilGasUnit = mineralCostUntilGasUnit + simulatedSlimBuild[g].mineralCost;
            }
            double timeOfNextGasUnit = (mineralCostUntilGasUnit - mineralBank) / mineralIncome;
            double minTime = simulatedSlimBuild[spotForward].minTime + timeDelayed - currentTime;
            if (minTime > timeOfNextGasUnit) { timeOfNextGasUnit = minTime; }
            if (timeOfNextGasUnit < 0) { timeOfNextGasUnit = 0; }
            return timeOfNextGasUnit;
        }
        public int findTotalGasCostXUnitsForward(List<int> spotOfNextGasUnits, int numGasUnitsForward)
        {
            int numSpotsToLook = numGasUnitsForward;
            int totalGasCost = 0;
            if (spotOfNextGasUnits.Count() < numSpotsToLook) { numSpotsToLook = spotOfNextGasUnits.Count(); }
            for (int x = 0; x < numSpotsToLook; x++)
            {
                totalGasCost = totalGasCost + simulatedSlimBuild[spotOfNextGasUnits[x]].gasCost;
            }
            return totalGasCost;
        }

        public List<int> findNextGasUnits(int spotInBuild)
        {
            List<int> spotOfNextGasUnits = new List<int>();
            for (int x = spotInBuild; x < simulatedSlimBuild.Count(); x++)
            {
                if (simulatedSlimBuild[x].gasCost > 0)
                {
                    spotOfNextGasUnits.Add(x);
                }
            }
            return spotOfNextGasUnits;
        }
        public int findGasSpotForward(List<int> spotOfNextGasUnits, int numGasUnitsForward)
        {
            int spotForward;
            if (numGasUnitsForward >= spotOfNextGasUnits.Count())
            {
                spotForward = spotOfNextGasUnits[spotOfNextGasUnits.Count() - 1];
            }
            else
            {
                spotForward = spotOfNextGasUnits[numGasUnitsForward - 1];
            }
            return spotForward;
        }
        public double calculateEventGenTerm(int spotInBuild)
        {
            //calculate general term
            //find build time of last unit
            double lastUnitBuildTime = pairObjectsLibrary.First(x => x.name.Equals(simulatedSlimBuild[simulatedSlimBuild.Count - 1].name)).duration;
            double timeLeft = simulatedSlimBuild[simulatedSlimBuild.Count - 1].minTime + timeDelayed + lastUnitBuildTime - currentTime;
            double eventGenTerm = timeLeft;

            //if event is warpgateresearch, give extreme priority (higher than everything but pylons)
            if (SlimBuild[spotInBuild].name.Equals("warpGateResearch"))
            {
                eventGenTerm = eventGenTerm * Math.Pow(10, 10);
            }
            return eventGenTerm;
        }
        public double calculateExpectedTimeUntilNextEvent(int spotInBuild)
        {
            //find mineral constraint (consider that if time<4 seconds, mineral income will be set at .001) ->
            double mineralConstraint;
            if (currentTime < 4)
            {
                double thisMineralIncome = calculateMineralIncome(1, 12);
                mineralConstraint = (simulatedSlimBuild[spotInBuild].mineralCost - mineralBank) / thisMineralIncome + 4;
            }
            else
            {
                mineralConstraint = (simulatedSlimBuild[spotInBuild].mineralCost - mineralBank) / mineralIncome;
            }

            //find gas constraint
            double gasConstraint = (simulatedSlimBuild[spotInBuild].gasCost - gasBank) / gasIncome;
            //find minTime constraint
            double minTimeConstraint = simulatedSlimBuild[spotInBuild].minTime + timeDelayed - currentTime;

            //return largest of the three
            double toReturn = mineralConstraint;
            if (gasConstraint > toReturn) { toReturn = gasConstraint; }
            if (minTimeConstraint > toReturn) { toReturn = minTimeConstraint; }
            if (0 > toReturn) { toReturn = 0; }
            return toReturn;
        }
        public void updateIncomeRates()
        {
            double effectiveProbeCountOnMinerals = buildingStatusTracker.probeCount - 3 * buildingStatusTracker.assimilatorCount;
            //assume each assimilator has 3 probes on it
            //if all probes would be on gas, leave one on minerals
            int effectiveProbeCountOnGas = 3 * buildingStatusTracker.assimilatorCount;
            if (buildingStatusTracker.probeCount == buildingStatusTracker.assimilatorCount * 3)
            {
                effectiveProbeCountOnGas = buildingStatusTracker.probeCount - 1;
                effectiveProbeCountOnMinerals = 1;
            }
            //gasIncome = 4 / 1.415 * effectiveProbeCountOnGas;
            gasIncome = 0.9 * effectiveProbeCountOnGas;

            //rest of probes on minerals
            //perform approximation for now
            if (buildingStatusTracker.probeCount - 3 * buildingStatusTracker.assimilatorCount > 20 * buildingStatusTracker.nexusCount)
            {
                effectiveProbeCountOnMinerals = 20 * buildingStatusTracker.nexusCount;
            }
            int probesLeft = buildingStatusTracker.probeCount - 3 * buildingStatusTracker.assimilatorCount;
            if (probesLeft > 20 * buildingStatusTracker.nexusCount)
            {
                if (probesLeft > 24 * buildingStatusTracker.nexusCount)
                {
                    effectiveProbeCountOnMinerals = 22 * buildingStatusTracker.nexusCount;
                }
                else
                {
                    effectiveProbeCountOnMinerals = probesLeft - (probesLeft - 20 * buildingStatusTracker.nexusCount) / 2;
                }
            }
            mineralIncome = effectiveProbeCountOnMinerals * 0.94;
        }
        public void updateTime(double timeToElapse)
        {
            if (recordBankStatements)
            {
                updateBankStatements(timeToElapse);
            }
            mineralBank = mineralBank + timeToElapse * mineralIncome;
            gasBank = gasBank + timeToElapse * gasIncome;
            currentTime = currentTime + timeToElapse;

        }
        private void updateBankStatements(double timeToElapse)
        {
            //need to add an entry at every second from current time to (currentTime + timeToElapse)
            int firstEntry = (int)Math.Ceiling(currentTime);

            //might need to overwrite first entry
            //if (firstEntry==0 && bankStatementList.Count() == 1)
            //{
            //    bankStatementList.RemoveAt(0);
            //}

            int lastEntry = (int)Math.Floor(currentTime + timeToElapse);
            for (int i = firstEntry; i < lastEntry + 1; i++)
            {
                //find mineral and gas count at this time and record
                double timeChange = i - currentTime;
                double mineralCount = mineralBank + mineralIncome * timeChange;
                double gasCount = gasBank + gasIncome * timeChange;
                //only create new entry if i==bankStatementList.Count()
                if (i == bankStatementList.Count())
                {
                    BankStatement newStatement = new BankStatement(mineralCount, gasCount);
                    bankStatementList.Add(newStatement);
                }
                else
                {
                    bankStatementList[i].mineralBank = mineralCount;
                    bankStatementList[i].gasBank = gasCount;
                }

            }
        }
        public buildOrderEntry performAction(string actionName, double minStartTime, List<buildOrderEntry> buildOrderInput, int methodLevel)
        {
            if (coefFailed)
            {
                buildOrderEntry dummyval = new buildOrderEntry("dummy", 0);
                return dummyval;
            }
            //get object data
            //action is either unit, building, (probe, assimilator, pylon)->need to log completion time
            bUUDataObject objectData = pairObjectsLibrary.First(x => x.name.Equals(actionName));
            int mineralCost = objectData.mineralCost;
            int gasCost = objectData.gasCost;
            bool needToRunUnfinishedEventLog = false;
            double amountOfTimeToUpdate;
            double timeUntilAfford;
            double timeUntilBuild = 0;

            //consider cases where need to run event log regardless of whenCanAfford

            //if dont have supply, action is probe and all nexi are queued,or have no gas income and object costs gas
            if (actionName.Equals("Probe") && buildingStatusTracker.freeNexusCount == 0 || currentSupply + objectData.supplyCost > currentSupplyCap || gasIncome == 0 && gasCost > 0)
            {
                //need a queued probe to finish before can build
                needToRunUnfinishedEventLog = true;
            }
            else
            {
                timeUntilAfford = findTimeUntilAfford(mineralCost, gasCost);
                //start at maximum of time until afford and (minStartTime+delayTime)
                timeUntilBuild = minStartTime + timeDelayed - currentTime;
                if (timeUntilAfford > timeUntilBuild)
                {
                    timeUntilBuild = timeUntilAfford;
                }
            }

            //find nextTime in unfinishedEventLog
            double timeUntilNextUnfinishedEvent = 100000;
            if (unfinishedEventLog.Any())
            {
                timeUntilNextUnfinishedEvent = unfinishedEventLog.OrderBy(x => x.timeToBuild).First().timeToBuild - currentTime;
            }

            //see if unfinishedEventLog will trigger before timeUntilBuild (only compare if needToRunUnfinishedEventLog is false)
            amountOfTimeToUpdate = timeUntilNextUnfinishedEvent;
            if (!needToRunUnfinishedEventLog && amountOfTimeToUpdate > timeUntilBuild)
            {
                //do not have to run unfinishedEventLog
                amountOfTimeToUpdate = timeUntilBuild;
                updateTime(amountOfTimeToUpdate);
            }
            else
            {
                //unfinishedEventLog needs to be run
                if (!unfinishedEventLog.Any())
                {
                    //if reaches this point, build has errored.
                    coefFailed = true;
                    buildOrderEntry dummyval = new buildOrderEntry("dummy", 0);
                    return dummyval;
                    int v = 0;
                }
                //first update time to next spot in unfinishedEventLog
                updateTime(amountOfTimeToUpdate);

                runUnfinishedEventLog(); //check agian if build errored
				if (coefFailed)
				{
					buildOrderEntry dummyval = new buildOrderEntry("dummy", 0);
					return dummyval;
				}
					//restart process, repeat until unfinishedEventLog doesn't have to be ran
					performAction(actionName, minStartTime, buildOrderInput, 1);
                //have already updated time and made purchase updates

            }
            //only reaches here if didn't need unfinishedEventLog


            //only perform purchase updates if highest level (level 0)
            if (methodLevel == 0)
            {
                makePurchaseUpdates(objectData, minStartTime);


            }

            //return does nothing if on inner level
            buildOrderEntry entryToReturn = new buildOrderEntry(actionName, currentTime);
            return entryToReturn;
        }
        public void makePurchaseUpdates(bUUDataObject objectData, double minStartTime)
        {
            string actionName = objectData.name;
            int mineralCost = objectData.mineralCost;
            int gasCost = objectData.gasCost;

            updateBuildingStatusVariables(actionName);

            //update supply
            currentSupply = currentSupply + objectData.supplyCost;

            //update time delayed if event occurs later than expected
            //if event was probe, nexus, assimilator, or a second pylon it does not affect time delayed
            //if event was a pylon and not the first pylon it shouldn't affect delayed time
            //check if realized build log already contains a pylon
            if (actionName.Equals("pylon") && realizedBuildLog.Where(x => x.itemToBuild.Equals("pylon")).Any())
            {
            }
            else if (actionName.Equals("Probe") || actionName.Equals("nexus") || actionName.Equals("assimilator"))
            {
            }
            else
            {
                if (currentTime > timeDelayed + minStartTime)
                {
                    timeDelayed = currentTime - (timeDelayed + minStartTime) + timeDelayed;
                }
            }

            //make purchase
            mineralBank = mineralBank - mineralCost;
            gasBank = gasBank - gasCost;

            //if item is probe make one less nexi free
            if (actionName.Equals("Probe"))
            {
                buildingStatusTracker.freeNexusCount--;
            }

            //if item needs to be added to unfinishedevent log
            if (actionName.Equals("assimilator") || actionName.Equals("pylon") || actionName.Equals("nexus") || actionName.Equals("Probe"))
            {
                //if chronoFirstProbe and in range, calculate new duration
                double duration = objectData.duration;
                if(chronoFirstProbe && currentTime>35 && currentTime < 56 && actionName.Equals("Probe"))
                {
                    double timeInRange = 56 - currentTime;
                    //probe takes 12 seconds to build
                    if (timeInRange > 8) { timeInRange = 8; }
                    duration = 12 - (timeInRange * 1.5) + timeInRange;

                }
                double timeOfCompletion = currentTime + duration;
                buildOrderEntry unfinishedEvent = new buildOrderEntry(actionName, timeOfCompletion);
                unfinishedEventLog.Add(unfinishedEvent);
            }
        }

        private void updateBuildingStatusVariables(string actionName)
        {
            actionName = appendDigits(actionName);
            //update futureProbe/NexusCount
            if (actionName.Equals("Probe")) { buildingStatusTracker.futureProbeCount++; }
            else if (actionName.Equals("nexus")) { buildingStatusTracker.futureNexusCount++; }
            else if (actionName.Equals("assimilator")) { buildingStatusTracker.futureAssimilatorCount++; }
            else if (actionName.Equals("gateway")) { buildingStatusTracker.gatewayCount++; }
            else if (actionName.Equals("warpGate")) { buildingStatusTracker.warpGateCount++; }
            else if (actionName.Equals("roboticsFacility")) { buildingStatusTracker.roboCount++; }
            else if (actionName.Equals("stargate")) { buildingStatusTracker.stargateCount++; }
        }
        public double findTimeUntilAfford(int mineralCost, int gasCost)
        {
            double timeUntilHaveMins;
            double timeUntilHaveGas;
            //find time until have mineralCost and gasCost to spare
            if (mineralBank >= mineralCost)
            {
                timeUntilHaveMins = 0;
            }
            else
            {
                timeUntilHaveMins = (mineralCost - mineralBank) / mineralIncome;
            }
            if (gasBank >= gasCost)
            {
                timeUntilHaveGas = 0;
            }
            else
            {
                timeUntilHaveGas = (gasCost - gasBank) / gasIncome;
            }
            //find largest of two times
            double timeUntilAfford = timeUntilHaveGas;
            if (timeUntilHaveMins > timeUntilHaveGas) { timeUntilAfford = timeUntilHaveMins; }
            return timeUntilAfford;
        }
        public void runUnfinishedEventLog()
        {
            //find next event in unFinishedEventLog and perform it (time has already been updated to its event time)
            buildOrderEntry thisEvent = unfinishedEventLog.OrderBy(x => x.timeToBuild).First();
            string thisEventName = thisEvent.itemToBuild;

            if (thisEventName.Equals("startProbeMining"))
            {
                updateIncomeRates();
            }
            else
            {
                //perform different operations for probes, pylons, nexi, assimilators
                //get object details
                bUUDataObject objectStats = pairObjectsLibrary.First(x => x.name.Equals(thisEventName));
                currentSupplyCap = currentSupplyCap + objectStats.supplyProvided;
                if (thisEventName.Equals("pylon"))
                {
                    buildingStatusTracker.pylonCount++;

                }
                else if (thisEventName.Equals("assimilator"))
                {
					//check if have more assimilators than allowed by nexi
					
                    buildingStatusTracker.assimilatorCount++;
					if (buildingStatusTracker.assimilatorCount > 2 * buildingStatusTracker.nexusCount)
					{
						coefFailed = true; return;
					}
					updateIncomeRates();
                }
                else if (thisEventName.Equals("nexus"))
                {
                    buildingStatusTracker.nexusCount++;
                    buildingStatusTracker.freeNexusCount++;
                    updateIncomeRates();
                    addNexusToTimeChronoAvailable(thisEvent.timeToBuild);
                }
                else
                {
                    //thisEvent=Probe
                    buildingStatusTracker.probeCount++;
                    buildingStatusTracker.freeNexusCount++;
                    updateIncomeRates();
                }
            }
            //remove event from unfinishedEventLog
            unfinishedEventLog.Remove(thisEvent);
        }
        public string appendDigits(string item)
        {
            var digits = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
            return item.TrimEnd(digits).ToString();
        }

        public void initializeBuildConditions(int startingProbeCount, int startingNexusCount, int startingPylonCount, int startingAssimilatorCount, double probeMineralMiningRate, double probeGasMiningRate)
        {
            currentTime = 0;
            timeDelayed = 0;
            mineralBank = 50;
            gasBank = 0;
            currentSupply = startingProbeCount;
            currentSupplyCap = startingNexusCount * pairObjectsLibrary.First(x => x.name.Equals("nexus")).supplyProvided + startingPylonCount * pairObjectsLibrary.First(x => x.name.Equals("pylon")).supplyProvided;

            buildingStatusTracker = new buildingStatusTrackerObject(startingProbeCount, startingNexusCount, startingAssimilatorCount);

            //add probe start mining unfinishedEvent
            unfinishedEventLog = new List<buildOrderEntry>();
            buildOrderEntry startProbeMining = new buildOrderEntry("startProbeMining", 4);
            unfinishedEventLog.Add(startProbeMining);
            realizedBuildLog = new List<buildOrderEntry>();

            //set mineral income to .0001 until startProbeMining event occurs (accounts for how probes all start at farthest position in mining process)
            mineralIncome = .0001;
            gasIncome = startingAssimilatorCount * 3 * probeGasMiningRate;
        }

        public void initializeSlimBuild(List<buildOrderEntry> buildOrder)
        {
            //fill slim Build
            SlimBuild = new List<ProcessFriendlyBuildObject>();
            
            foreach (buildOrderEntry thisItem in buildOrder)
            {
                bUUDataObject objectRef = pairObjectsLibrary.First(x => x.name.Equals(thisItem.itemToBuild));
                ProcessFriendlyBuildObject newObject = new ProcessFriendlyBuildObject(objectRef.mineralCost, objectRef.gasCost, objectRef.supplyCost, objectRef.supplyProvided, thisItem.itemToBuild, thisItem.timeToBuild);
                SlimBuild.Add(newObject);
            }


        }
        public double calculateMineralIncome(double nexusCount, double probeCount)
        {
            //assume economy caps at 20 probes per base for now
            //give probes 20-24 half income
            double effectiveProbeCountOnMinerals = probeCount;
            if (probeCount > 20 * nexusCount)
            {
                if (probeCount > 24 * nexusCount)
                {
                    effectiveProbeCountOnMinerals = 22 * nexusCount;
                }
                else
                {
                    effectiveProbeCountOnMinerals = probeCount - (probeCount - 20 * nexusCount) / 2;
                }
            }

            return effectiveProbeCountOnMinerals * 0.94;
        }
        private double calculateGasIncome(int numProbesOnGas)
        {
            return numProbesOnGas * 0.9;
        }
        public void initializeCoefficients(coefficientPackage thisCoefficientPackage)
        {
            prVals = thisCoefficientPackage.prVals;
            aVals = thisCoefficientPackage.aVals;
            eVals = thisCoefficientPackage.eVals;
            pyVals = thisCoefficientPackage.pyVals;
            nVals = thisCoefficientPackage.nVals;
        }

        public void initializeTimeDelayedTracker()
        {
            timeDelayedTracker = new timeDelayedTrackerObject();
        }
    }
}
