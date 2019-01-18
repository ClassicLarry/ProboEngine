using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class chronoOptionEvaluation
    {
        public bool discardBuild { get; set; }
        public List<bUUEvent> chronoEventList { get; set; }
        private List<bUUEvent> orderedChronoEventList { get; set; }
        private List<bUUDataObject> pairObjectLibrary { get; set; }
        public List<buildOrderEntry> chronoEcoLog { get; set; }
        public List<buildOrderEntry> chronoWGLog { get; set; }
        public List<buildOrderEntry> chronoCELog { get; set; }
        private List<double> approximateChronoTimes { get; set; }
        private List<int> WGREventChain { get; set; }
        private bool WGIterationDone { get; set; }
        private List<IDWithList> lastBasketEntries { get; set; }
        private List<IDWithList> precedenceRelations { get; set; }
        private List<PairsTable> originalPairList { get; set; }
        public double buildEndTime { get; set; }
        public chronoOptionEvaluation(double globalFastestBuildYet, int optionNum, List<int> thisChronoOption, List<double> originalMineralStatements, List<double> gasStatements, List<double> emptyMineralStatements, List<bUUEvent> originalEventList, List<bUUEvent> originalOrderedEventList, List<bUUEvent> myChronoEventList, List<bUUEvent> myOrderedChronoEventList, int WGRSpot, List<bUUDataObject> myPairObjectLibrary, List<double> myApproximateChronoTimes, List<int> myWGREventChain, List<IDWithList> myLastBasketEntries, List<IDWithList> myPrecedenceRelations, List<PairsTable> myOriginalPairList, int globalIterationNum, bool firstChronoSelected)
        {
            //1. Initialize Properties
            orderedChronoEventList = myOrderedChronoEventList;
            originalPairList = myOriginalPairList;
            precedenceRelations = myPrecedenceRelations;
            lastBasketEntries = myLastBasketEntries;
            WGREventChain = myWGREventChain;
            approximateChronoTimes = myApproximateChronoTimes;
            List<PairsTable> chronoPairList = new List<PairsTable>();
            List<int> firstWGEvents = new List<int>();
            discardBuild = false;
            chronoEventList = myChronoEventList;
            pairObjectLibrary = myPairObjectLibrary;

            //2. Reset chronoEventDetails if neccessary
            if (optionNum != 0) { resetChronoEventList(originalEventList); }

            //3. Implement economic chronos (also creates lastECOChronoLog)
            List<double> mineralStatements = adjustFreeCashFromChrono(emptyMineralStatements, originalMineralStatements, thisChronoOption, globalIterationNum, firstChronoSelected);

            //3.5 discard build if it will be impossible to afford all elements by globalFastestBuildYet-quickestUnitBuildTime
            if (checkIfDiscardBasedOnAfford(globalFastestBuildYet, mineralStatements, gasStatements, WGRSpot))
            {
                buildEndTime = 10000000; return;
            }

            //4. Left Shift build to fit new eco chart based on economic chronos 
            double newTimeShifted = leftShiftEntireBuild(originalOrderedEventList, mineralStatements, gasStatements);
            if (discardBuild) { buildEndTime = 100000000; return; }

            //5. shift times in cchronoEventList based on originalEventList and timeShifted
            createShiftedEventList(originalEventList, newTimeShifted);

            //only perform steps 6 and 7 if build requires warpgate and gatewayunits
            int gatewayBUUDataID = pairObjectLibrary.First(x => x.name.Equals("gateway")).bUUDataID;
            bool includeGatewayUnits = lastBasketEntries.Where(x => x.ID == gatewayBUUDataID).Any();
            if (WGRSpot >= 0 && includeGatewayUnits)
            {
                //6. Left shift WGR Component of build and shorten time based on WGR chronos. Discard build if chrono is used on WG after WG is done
                leftShiftWGRComponents(originalOrderedEventList, thisChronoOption, mineralStatements, gasStatements, WGRSpot);
                if (discardBuild) { buildEndTime = 100000000; return; }
				//check if twi is before warpgateresearch
				//7. Perform WG transformation iterative process. (this may change ordering of events)
				firstWGEvents = performWGTransformationProcess(WGRSpot, newTimeShifted, mineralStatements, gasStatements);
				//check if twi is before warpgateresearch

				//check if a warpgate event is before warpgate is done
				double WGRDoneAt = chronoEventList[pairObjectLibrary[WGRSpot].bUUEventIDForPrimaryBuilding].endTime;
                if (chronoEventList.Where(x => x.startTime < WGRDoneAt && x.fromWG).Any())
                {
                    orderedChronoEventList = orderedChronoEventList;
                    leftShiftWGRComponents(originalOrderedEventList, thisChronoOption, mineralStatements, gasStatements, WGRSpot);
                }
				//8. Add in warpgates to chronoEventList, create seperate "chronoPairList"
				addWGEvents(chronoPairList, firstWGEvents);
            }
            //9. Perform remaining chronos on chain ends 
			//What if chrono boosting a chain end means more units should be made from Warpgate
            performCEChronos(globalFastestBuildYet, chronoPairList, thisChronoOption, mineralStatements, gasStatements, WGRSpot);

			//10. Record EndTime
			buildEndTime = chronoEventList.Max(x => x.endTime);
        }
        private bool checkIfDiscardBasedOnAfford(double minTime, List<double> mineralStatements, List<double> gasStatements, int WGRSpot)
        {
            //account for if time over 500
            if (minTime >= 500) { minTime = 499; }

            //find duration of shortest unit Event
            double shortestUnitDuration = 1000;
            //if build includes WG, shortest unit is 3.6 seconds
            if (WGRSpot >= 0) { shortestUnitDuration = 3.6; }
            else
            {
                foreach (bUUEvent thisEvent in chronoEventList)
                {
                    if (pairObjectLibrary[thisEvent.bUUDataID].duration < shortestUnitDuration && !pairObjectLibrary[thisEvent.bUUDataID].isBuilding)
                    {
                        shortestUnitDuration = pairObjectLibrary[thisEvent.bUUDataID].duration;
                    }
                }
            }
            //assume that this unit will be chrono boosted
            int timeNeedAfford = (int)Math.Ceiling(minTime - shortestUnitDuration / 1.5);

            //find total mineral and gas cost
            int totalMineralCost = 0;
            int totalGasCost = 0;
            foreach (bUUEvent thisEvent in chronoEventList)
            {
                totalMineralCost = totalMineralCost + pairObjectLibrary[thisEvent.bUUDataID].mineralCost;
                totalGasCost = totalGasCost + pairObjectLibrary[thisEvent.bUUDataID].gasCost;
            }

            //check if have all neccessary resources at timeNeedAfford
            if (totalMineralCost > mineralStatements[timeNeedAfford]) { return true; }
            if (totalGasCost > gasStatements[timeNeedAfford]) { return true; }
            return false;
        }
        private void resetChronoEventList(List<bUUEvent> originalEventList)
        {
            //reset cchronoEventList startTimes, endTimes, Remove WG Events, Fix Pair labeling for parent and child of WG
            //also update 

            //1. Remove WG events. (they will all be at the end)
            //also remove these events from semiOrderedChronoList
            for (int i = chronoEventList.Count() - 1; i >= 0; i--)
            {
                //check if has no mineral cost (must be warpgate then)
                if (pairObjectLibrary[chronoEventList[i].bUUDataID].mineralCost == 0)
                {
                    chronoEventList.RemoveAt(i);
                }
                else { break; }
            }
            for (int i = orderedChronoEventList.Count() - 1; i >= 0; i--)
            {
                if (pairObjectLibrary[orderedChronoEventList[i].bUUDataID].mineralCost == 0)
                {
                    orderedChronoEventList.RemoveAt(i);
                }
            }

            //reset pair and timings
            for (int i = 0; i < chronoEventList.Count(); i++)
            {
                chronoEventList[i].startTime = originalEventList[i].startTime;
                chronoEventList[i].endTime = originalEventList[i].endTime;
                chronoEventList[i].pairAsChildID = originalEventList[i].pairAsChildID;
                chronoEventList[i].pairAsParentID = originalEventList[i].pairAsParentID;
                chronoEventList[i].fromWG = false;
            }
        }
        private List<double> adjustFreeCashFromChrono(List<double> mineralFunction, List<double> originalMineralFunction, List<int> thisChronoOption, int globalIterationNum, bool firstChronoSelected)
        {
            //approximate the effect that a chrono boost will have on the economy
            //Chrono Details: 50% speed increase for 20 seconds.
            //Probe Details: 12 second build time, 50 minerals
            //During those 20 seconds 1.67 probes would have been built
            //Now 2.5 probes will be built
            //Net gain in probes: .8333 Probes
            //Additional 50*.8333 minerals spent
            //if chrono is used immediately:
            //first probe mines for 4 seconds more
            //second probe mines for 8 seconds more
            //additional .8333 probes total
            chronoEcoLog = new List<buildOrderEntry>();
            int numElements = originalMineralFunction.Count();
            for (int i = 0; i < numElements; i++)
            {
                mineralFunction[i] = originalMineralFunction[i];
            }
            //perform economic shift only if globalIterationNum==0 or firstChronoSelected==false
            //if firstchronoSelected, then need to log in book but dont shift
            if (firstChronoSelected)
            {
                buildOrderEntry newEntry1 = new buildOrderEntry("nexus", 36);
                chronoEcoLog.Add(newEntry1);
            }
            if (globalIterationNum != 0 && firstChronoSelected) { return mineralFunction; }

            for (int i = 0; i < thisChronoOption.Count(); i++)
            {
                if (thisChronoOption[i] == 0)
                {
                    //log economic chrono
                    //hard code to 36 seconds for first chrono
                    buildOrderEntry newEntry = new buildOrderEntry("nexus", 36);
                    chronoEcoLog.Add(newEntry);

                    mineralFunction = performEconomicShift(mineralFunction, i);
                }
            }
            return mineralFunction;
        }
        private List<double> performEconomicShift(List<double> alteredCashFunction, int chronoToUse)
        {

            //Decrease free cash by 40 minerals after 10 seconds
            //after 20 seconds, increase all future times by .8*t

            //perform manipulation at 36 seconds
            double chronoTime = 36;
            //double chronoTime = approximateChronoTimes[chronoToUse];
            int nearestTime = (int)Math.Floor(chronoTime) + 10;
            int numItems = alteredCashFunction.Count();
            alteredCashFunction[nearestTime] = alteredCashFunction[nearestTime] - 40;
            //make all previous times above this amount, this amount
            for (int g = nearestTime - 1; g >= 0; g--)
            {
                if (alteredCashFunction[g] > alteredCashFunction[nearestTime])
                {
                    alteredCashFunction[g] = alteredCashFunction[nearestTime];
                }
                else { break; }
            }
            //make from t=10, through t=19 also have decrease
            for (int g = nearestTime + 1; g < nearestTime + 10; g++)
            {
                alteredCashFunction[g] = alteredCashFunction[g] - 40;
            }
            //make remaining times include .8*t
            double valueToUse = -40 - 8 - nearestTime * .8;
            for (int g = nearestTime + 10; g < numItems; g++)
            {
                //alteredCashFunction[g].mineralBank = alteredCashFunction[g].mineralBank - 40 + (g-10-nearestTime) * .8;
                alteredCashFunction[g] = alteredCashFunction[g] + valueToUse + .8 * g;
            }

            return alteredCashFunction;
        }
        private double leftShiftEntireBuild(List<bUUEvent> originalOrderedEventList, List<double> mineralStatements, List<double> gasStatements)
        {
            int totalMineralsSpent = 0;
            int totalGasSpent = 0;
            //go through entire event list, shift events until meet criteria
            //first order eventList by startTime
            //do initial shift to where all eventTimes are >=0
            double timeShifted = -originalOrderedEventList[0].startTime;
            foreach (bUUEvent thisEvent in originalOrderedEventList)
            {
                //update totalMinerals and gas spent
                totalMineralsSpent = totalMineralsSpent + pairObjectLibrary[thisEvent.bUUDataID].mineralCost;
                totalGasSpent = totalGasSpent + pairObjectLibrary[thisEvent.bUUDataID].gasCost;
                //check if can afford at startTime+timeShifted
                //if not, increase timeShifted
                //timeShifted = timeShifted + findTimeUntilAfford(totalMineralsSpent, totalGasSpent, thisEvent.startTime, timeShifted, mineralStatements, gasStatements);
                timeShifted = timeShifted + findTimeUntilAfford(totalMineralsSpent, totalGasSpent, thisEvent.startTime, timeShifted, mineralStatements, gasStatements);
                if (discardBuild) { buildEndTime = 100000000; return timeShifted; }
            }
            return timeShifted;
        }
        private double findTimeUntilAfford(int totalMineralsSpent, int totalGasSpent, double eventStartTime, double timeShifted, List<double> mineralStatements, List<double> gasStatements)
        {
            //first check if condition is met at currentTime
            int nearestTime = (int)(eventStartTime + timeShifted);

            //if nearest Time is over 500, dead build
            if (nearestTime >= 500) { discardBuild = true; return 1000000; }
            bool mineralCondition = (mineralStatements[nearestTime] > totalMineralsSpent);
            bool gasCondition = (gasStatements[nearestTime] > totalGasSpent);
            if (mineralCondition && gasCondition) { return 0; }

            else if (!mineralCondition && !gasCondition)
            {
                int earliestMineralTime = inverseFreeCashFunction(nearestTime, totalMineralsSpent, mineralStatements);
                int earliestGasTime = inverseFreeCashFunction(nearestTime, totalGasSpent, gasStatements);
                if (earliestMineralTime > earliestGasTime) { return earliestMineralTime; }
                else { return earliestGasTime; }
            }
            else if (!mineralCondition)
            {
                return inverseFreeCashFunction(nearestTime, totalMineralsSpent, mineralStatements);
            }
            else //only doesn't meet gas condition
            {
                return inverseFreeCashFunction(nearestTime, totalGasSpent, gasStatements);
            }
        }

        private int inverseFreeCashFunction(int earliestTime, int totalMineralsSpent, List<double> freeCashFunction)
        {
            //find the time when have total Minerals Spent
            //go by 5 second intervals forward, and then backtrack once have money
            int stepSize = 16;
            if (totalMineralsSpent == 0) { return 0; }
            for (int i = 0; i < 10000; i++)
            {
                int val = i * stepSize + earliestTime;

                //check if gone out of 1000 bound
                if (val >= 1000) { discardBuild = true; return 100000; }
                if (freeCashFunction[val] >= totalMineralsSpent)
                {
                    //now step back by 8 
                    if (freeCashFunction[val - 8] >= totalMineralsSpent)
                    {
                        //step back by 4 again
                        if (freeCashFunction[val - 12] >= totalMineralsSpent)
                        {
                            //work backwords 4 steps
                            for (int j = val - 13; j >= 0; j--)
                            {
                                if (freeCashFunction[j] < totalMineralsSpent) { return j + 1 - earliestTime; }
                            }
                        }
                        else
                        {
                            //work forwards 4 steps
                            for (int j = val - 11; j < val + 40; j++)
                            {
                                if (freeCashFunction[j] > totalMineralsSpent) { return j - earliestTime; }
                            }
                        }
                    }
                    else
                    {
                        //step forward by 4
                        if (freeCashFunction[val - 4] >= totalMineralsSpent)
                        {
                            //work backwards 4 steps
                            for (int j = val - 5; j >= 0; j--)
                            {
                                if (freeCashFunction[j] < totalMineralsSpent) { return j + 1 - earliestTime; }
                            }
                        }
                        else
                        {
                            //work forwards 4 steps
                            for (int j = val - 3; j < val + 40; j++)
                            {
                                if (freeCashFunction[j] > totalMineralsSpent) { return j - earliestTime; }
                            }
                        }
                    }
                }
            }
            return -1;
        }
        private void createShiftedEventList(List<bUUEvent> originalEventList, double timeShifted)
        {
            //update the timing of cchronoEventList based on timeShifted and the original eventList
            for (int i = 0; i < chronoEventList.Count(); i++)
            {
                chronoEventList[i].startTime = originalEventList[i].startTime + timeShifted;
                chronoEventList[i].endTime = originalEventList[i].endTime + timeShifted;
            }
        }
        private void leftShiftWGRComponents(List<bUUEvent> originalOrderedEventList, List<int> thisChronoOption, List<double> mineralStatements, List<double> gasStatements, int WGRSpot)
        {
			//create a log of when each chrono Boost is used
			chronoWGLog = new List<buildOrderEntry>();
            double chronoBoostDuration = 20; //lasts for 20 seconds
            double chronoBoostImpact = 1.5; //50% speed increase
                                            //consider only pylon, gateway, cybercore, and WGR
                                            //shift these elements as far left as possible

            double timeWGRShifted = -chronoEventList[WGREventChain[0]].startTime;
            //check pylon, then gateway, then cyber, then wgr
            foreach (int thisEvent in WGREventChain)
            {
                //find total amount spent on all earlier events
                //go through ordered eventList up to this event
                int totalMineralsSpent = findTotalMineralsSpent(thisEvent, originalOrderedEventList);
                int totalGasSpent = findTotalGasSpent(thisEvent, originalOrderedEventList);

                //need to start below final time and figure out how far right to shift to reach final time.
                //start at time where pylon T=0
                int newTime = (int)Math.Floor(chronoEventList[thisEvent].startTime);
                timeWGRShifted = timeWGRShifted + findTimeUntilAfford(totalMineralsSpent, totalGasSpent, newTime, timeWGRShifted, mineralStatements, gasStatements);
            }
            //shift all event
            foreach (int thisEvent in WGREventChain)
            {
                chronoEventList[thisEvent].startTime = chronoEventList[thisEvent].startTime + timeWGRShifted;
                chronoEventList[thisEvent].endTime = chronoEventList[thisEvent].endTime + timeWGRShifted;
            }
            //find WGR completion time
            int WGREventID = pairObjectLibrary[WGRSpot].bUUEventIDForPrimaryBuilding;
            double WGRDone = chronoEventList[WGREventID].endTime;
            double WGRStart = chronoEventList[WGREventID].startTime;

            //go through chrono options and find each option that is (1) WGR
            //can only use chronos that cover WGR time
            double WGRChronoedUntil = 0;
			//if not 4 chronos on WGR, start chrono 10 seconds late so failure to converge doesn't have chrono before real start
			int numWGRChrono = 0;
			for (int i = 0; i < thisChronoOption.Count(); i++)
			{
				if (thisChronoOption[i] == 1) { numWGRChrono++; }
			}
			int extra = 0;
			if (numWGRChrono < 4) { extra = 10; }
			double nextWGROpenChrono = WGRStart+extra;
            for (int i = 0; i < thisChronoOption.Count(); i++)
            {
                if (thisChronoOption[i] == 1)
                {
                    //find when this chrono is available
                    double chronoReady = approximateChronoTimes[i];

                    double chronoActualTime;
                    if (nextWGROpenChrono > chronoReady) //chronoBoost at nextWGROpenChrono
                    {
                        chronoActualTime = nextWGROpenChrono;
                    }
                    else //chronoBoost once ready
                    {
                        chronoActualTime = chronoReady;
                    }
                    //make sure that chronoActualTime is before WGRDone
                    if (chronoActualTime <= WGRDone)
                    {
                        //find out how long chrono will be implemented for
                        //will last for all 20 seconds if wg has at least 30 seconds left
                        if ((WGRDone - chronoActualTime) / chronoBoostImpact >= chronoBoostDuration)
                        {
                            //last for entire duration
                            WGRDone = WGRDone - (chronoBoostDuration * chronoBoostImpact - chronoBoostDuration);
                            //update nextWGROpenChrono by full duration
                            nextWGROpenChrono = chronoActualTime + chronoBoostDuration;
                        }
                        else
                        {
                            //chrono boost will only last for part of the duration
                            double timeChronoed = (WGRDone - chronoActualTime) / chronoBoostImpact;
                            WGRDone = WGRDone - (timeChronoed * chronoBoostImpact - timeChronoed);
                            //update nextWGROpenChrono by full duration (bc another chrono wont be available anyways)
                            nextWGROpenChrono = chronoActualTime + chronoBoostDuration;
                        }
                        //log chrono time
                        buildOrderEntry newEntry = new buildOrderEntry(pairObjectLibrary[chronoEventList[WGREventID].bUUDataID].name, chronoActualTime);
                        chronoWGLog.Add(newEntry);
                    }
                    else
                    {
                        //this build is using a chronoBoost on WGR after WGR is already done. Discard Build.
                        //discardBuild = true; break;
                    }
                }
            }
            chronoEventList[WGREventID].endTime = WGRDone;
        }
        private int findTotalMineralsSpent(int eventID, List<bUUEvent> orderedEventList)
        {
            //go through ordered event list up through eventID
            int totalMineralsSpent = 0;
            for (int i = 0; i < orderedEventList.Count(); i++)
            {
                totalMineralsSpent = totalMineralsSpent + pairObjectLibrary[orderedEventList[i].bUUDataID].mineralCost;
                if (orderedEventList[i].bUUEventID == eventID) { break; }
            }
            return totalMineralsSpent;
        }
        private int findTotalGasSpent(int eventID, List<bUUEvent> orderedEventList)
        {
            //go through ordered event list up through eventID
            int totalGasSpent = 0;
            for (int i = 0; i < orderedEventList.Count(); i++)
            {
                totalGasSpent = totalGasSpent + pairObjectLibrary[orderedEventList[i].bUUDataID].gasCost;
                if (orderedEventList[i].bUUEventID == eventID) { break; }
            }
            return totalGasSpent;
        }

        private List<int> performWGTransformationProcess(int WGRSpot, double buildLength, List<double> mineralStatements, List<double> gasStatements)
        {
            int WGREventID = pairObjectLibrary[WGRSpot].bUUEventIDForPrimaryBuilding;
            double WGREndTime = chronoEventList[WGREventID].endTime;

            //1. find gateway units that are produced after WGR is done. (label these events as warpGate events)
            //2. produce these units out of warpgates instead (this will shorten these chain lengths) 
            //3. Right shift WG chains and secondary gateways to end Time
            transferGatewayUnitsToWG(WGREndTime);
            List<int> firstWGEvents = new List<int>();

            for (int i = 0; i < 100; i++)
            {
                //4. Identify how far pylon, primary gateway, cyberCore, and WGR could be right shifted (due to WG units now being at later times)
                //dont consider econ, just precedence and child constraints
                //also consider WGREndTime vs warpgate events (dont allow shift to be more than gap between first WG event and WGREndTime)
                double wiggleRoom = identifyWGRComponentsWiggleRoom(chronoEventList);


                //check amount leftShifted for debugging
                double spotBefore = chronoEventList[chronoEventList.Count() - 1].startTime;

                //5. Attempt to left shift rest of build based on econ up to wiggleRoom(wiggle room defines maximum shift without breaking precedence constraints)
                leftShiftEntireChronoBuild(wiggleRoom, mineralStatements, gasStatements);

                double distanceShifted = spotBefore - chronoEventList[chronoEventList.Count() - 1].startTime;
                //6. Transfer additional units to WG if neccessary
                WGIterationDone = true;
                firstWGEvents = transferNewGatewayUnitsToWG(WGREndTime);
                if (WGIterationDone) { break; }
            }

            return firstWGEvents;
        }
        private double identifyWGMaxShift(double WGREndTime, List<bUUEvent> thisEventList)
        {
            //compare WGR end time to first WGR Event (allow 7 seconds for WG to be built
            double firstWGEventTime = 10000;
            foreach (bUUEvent thisEvent in thisEventList)
            {
                if (thisEvent.fromWG && thisEvent.startTime < firstWGEventTime)
                {
                    firstWGEventTime = thisEvent.startTime;
                }
            }
            return firstWGEventTime - 7 - WGREndTime;
        }
        private void transferGatewayUnitsToWG(double WGREndTime)
        {
            //start at last basket entries for gateway
            //work down chains until reach chain element where its start time is after WGR endTime
            //this element should be produced out of a warpgate
            //should its parent be produced out of warpGate? For now assume not
            //for now dont add warpgate event or pair, just adjust times
            double buildEndTime = chronoEventList.Max(x => x.endTime);
            //repeat down entire chain. make final unit have duration of 3.6
            int gatewayBUUDataID = pairObjectLibrary.First(x => x.name.Equals("gateway")).bUUDataID;
            IDWithList gatewayRow = lastBasketEntries.First(x => x.ID == gatewayBUUDataID);
            foreach (int thisGatewayUnit in gatewayRow.IDList)
            {
                bool WGAdded = false;
                int lastID = 0; //will be overwritten before used
                int nextID = thisGatewayUnit;
                for (int i = 0; i < 1000; i++)
                {
                    if (WGAdded)
                    {
                        chronoEventList[nextID].startTime = chronoEventList[lastID].endTime;
                        chronoEventList[nextID].endTime = chronoEventList[nextID].startTime + pairObjectLibrary[chronoEventList[nextID].bUUDataID].durationWithwarpGateResearch;
                        chronoEventList[nextID].fromWG = true;
                        lastID = nextID;
                    }
                    else
                    {
                        //go down entire gatewayChain
                        //check if startTime after WGRDone
						//TRY shifting to warpgate if WGR is done or will be done in 7 seconds (7 is an estimate of at what point it would be worthwhile to produce out of WG)
                        if (chronoEventList[nextID].startTime + 0 >= WGREndTime)
                        {
                            WGAdded = true;
							double timeToWait = WGREndTime - chronoEventList[nextID].startTime;
							if (timeToWait < 0) { timeToWait = 0; }
							//shift first warpgate unit startTime back by 7 seconds
							//shift endTime by startTime + WGDuration
							chronoEventList[nextID].startTime = chronoEventList[nextID].startTime + 7 + timeToWait;
                            chronoEventList[nextID].endTime = chronoEventList[nextID].startTime + pairObjectLibrary[chronoEventList[nextID].bUUDataID].durationWithwarpGateResearch;
                            chronoEventList[nextID].fromWG = true;
                            lastID = nextID;
                        }
                    }
                    if (chronoEventList[nextID].pairAsParentID < 0) { break; }
                    nextID = originalPairList[chronoEventList[nextID].pairAsParentID].childBUUEventID;
                }
                //log lastID as final unit in this chain
                //make final unit have build time of 3.6 seconds
                if (WGAdded)
                {
                    chronoEventList[lastID].endTime = chronoEventList[lastID].startTime + 3.6;
                    double timeToShift = buildEndTime - chronoEventList[lastID].endTime;
                    //run back up chain and right shift everything to buildEndTime
                    for (int i = 0; i < 1000; i++)
                    {
                        chronoEventList[lastID].startTime = chronoEventList[lastID].startTime + timeToShift;
                        chronoEventList[lastID].endTime = chronoEventList[lastID].endTime + timeToShift;
                        lastID = originalPairList[chronoEventList[lastID].pairAsChildID].parentBUUEventID;
                        //check if lastID is gateway
                        if (pairObjectLibrary[chronoEventList[lastID].bUUDataID].isBuilding) { break; }
                    }
                    //shift if secondary building
                    if (thisGatewayUnit != gatewayRow.IDList[0])
                    {
                        chronoEventList[lastID].startTime = chronoEventList[lastID].startTime + timeToShift;
                        chronoEventList[lastID].endTime = chronoEventList[lastID].endTime + timeToShift;
                    }
                }

            }
        }
        public double identifyWGRComponentsWiggleRoom(List<bUUEvent> thisEventList)
        {
            //5. See how far right shifted pylon, primary gateway, cyberCore, and WGR can be(due to WG units now being at later times)
            //dont consider econ, just precedence and child constraints
            double buildEndTime = thisEventList.Max(x => x.endTime);
            double smallestShift = 100000;
            for (int i = WGREventChain.Count() - 2; i >= 0; i--)
            {
                //see how far this event can be right shifted based on precedence relations. 
                //for gateway also consider startTime of unit in lastBasketEntries
                List<int> precedenceEvents = precedenceRelations.First(x => x.ID == thisEventList[WGREventChain[i]].bUUEventID).IDList;
                double minEndTime = buildEndTime;
                foreach (int thisEvent in precedenceEvents)
                {
                    //dont consider other WGREvents
                    if (!WGREventChain.Contains(thisEvent))
                    {
                        if (thisEventList[thisEvent].startTime < minEndTime)
                        {
                            minEndTime = thisEventList[thisEvent].startTime;
                        }
                    }
                }
                //if primary gateway, check last basket entry
                if (i == 1) //gateway is at spot 1
                {
                    int firstGatewayUnitEventID = lastBasketEntries.First(x => x.ID == thisEventList[WGREventChain[1]].bUUDataID).IDList[0];
                    if (thisEventList[firstGatewayUnitEventID].startTime < minEndTime)
                    {
                        minEndTime = thisEventList[firstGatewayUnitEventID].startTime;
                    }
                }

                //record how far this event can be shifted
                double timeToShift = minEndTime - thisEventList[WGREventChain[i]].endTime;
                if (timeToShift < smallestShift) { smallestShift = timeToShift; }
            }
            //dont shift components so far that a warpgate unit is produced before WGR is done
            //find when WGR is done
            double WGREndTime = chronoEventList[WGREventChain[3]].endTime;
            double WGMaxShift = identifyWGMaxShift(WGREndTime, thisEventList);
            if (WGMaxShift < smallestShift) { smallestShift = WGMaxShift; }

            //dont shift so that a unit has negative build time
            foreach (bUUEvent thisEvent in chronoEventList)
            {
                if (!WGREventChain.Contains(thisEvent.bUUEventID))
                {
                    if (thisEvent.startTime - smallestShift < 0 && thisEvent.startTime > 0)
                    {
                        smallestShift = thisEvent.startTime;
                    }
                }
                //timeShifted = timeShifted + findTimeUntilAfford(totalMineralsSpent, totalGasSpent, thisEvent.startTime, timeShifted, mineralStatements, gasStatements);
            }



            return smallestShift;
        }
        private void leftShiftEntireChronoBuild(double wiggleRoom, List<double> mineralStatements, List<double> gasStatements)
        {
            compareBUUEvents c = new compareBUUEvents();
            orderedChronoEventList.Sort(c);
            //go through every item in order of startTime, put each startTime at earliest possible spot given minerals and gas
            int totalMineralsSpent = 0;
            int totalGasSpent = 0;
            //go through entire event list, shift events until meet criteria (dont shift WGR components)
            //first order eventList by startTime
            //do initial shift to where all eventTimes are >=0
            double timeShifted = -wiggleRoom;
            foreach (bUUEvent thisEvent in orderedChronoEventList)
            {
                //update totalMinerals and gas spent
                totalMineralsSpent = totalMineralsSpent + pairObjectLibrary[thisEvent.bUUDataID].mineralCost;
                totalGasSpent = totalGasSpent + pairObjectLibrary[thisEvent.bUUDataID].gasCost;
                //only perform following operation for non-WG components
                //actually need to perform on all 
                //check if can afford at startTime+timeShifted
                //if not, increase timeShifted
                if (!WGREventChain.Contains(thisEvent.bUUEventID))
                {
                    timeShifted = timeShifted + findTimeUntilAfford(totalMineralsSpent, totalGasSpent, thisEvent.startTime, timeShifted, mineralStatements, gasStatements);
                }
                //timeShifted = timeShifted + findTimeUntilAfford(totalMineralsSpent, totalGasSpent, thisEvent.startTime, timeShifted, mineralStatements, gasStatements);
            }
            if (timeShifted == 0)
            {
                //return chronoEventList; 
            }
            else
            {
                //apply timeShifted to all elements
                foreach (bUUEvent thisEvent in chronoEventList)
                {
                    if (!WGREventChain.Contains(thisEvent.bUUEventID))
                    {
                        thisEvent.startTime = thisEvent.startTime + timeShifted;
                        thisEvent.endTime = thisEvent.endTime + timeShifted;
                    }
                }
                //return chronoEventList;
            }
            //order again in case order shifted (may need to look at this part later, if events should be shifted again based on new order)
            //removing this for now
        }

        private List<int> transferNewGatewayUnitsToWG(double WGREndTime)
        {
            //return first WG events
            List<int> firstWGEvents = new List<int>();
            //start at last basket entries for gateway
            //work down chains until reach chain element where its start time is after WGR endTime
            //this element should be produced out of a warpgate. Check if it is already produced out of a warpgate.
            //If not, produce out of warpgate ->go down chain and make everything else warpgatetiming and right after previous unit
            //should its parent be produced out of warpGate? For now assume not
            //for now dont add warpgate event or pair, just adjust times
            //event with last start time will be a chain end which should end when build ends 
            double buildEndTime = orderedChronoEventList.Last().endTime;
            //repeat down entire chain. make final unit have duration of 3.6
            int gatewayBUUDataID = pairObjectLibrary.First(x => x.name.Equals("gateway")).bUUDataID;
            IDWithList gatewayRow = lastBasketEntries.First(x => x.ID == gatewayBUUDataID);
            foreach (int thisGatewayUnit in gatewayRow.IDList)
            {
                bool WGAdded = false;
                int lastID = 0; //will be overwritten before used
                int nextID = thisGatewayUnit;
                for (int i = 0; i < 1000; i++)
                {
                    if (WGAdded)
                    {
                        chronoEventList[nextID].startTime = chronoEventList[lastID].endTime;
                        chronoEventList[nextID].endTime = chronoEventList[nextID].startTime + pairObjectLibrary[chronoEventList[nextID].bUUDataID].durationWithwarpGateResearch;
                        chronoEventList[nextID].fromWG = true;
                        lastID = nextID;
                    }
                    else
                    {
                        //go down entire gatewayChain
                        //check if startTime after WGRDone
                        //if first unit from WG is up, its start time will be 7 seconds later since WG has to be built
                        double WGBuildTime = 0;
                        if (chronoEventList[nextID].fromWG) { WGBuildTime = 7; }
						//adding 7 to if statement below, because any unit produced within 7 seconds of wgr done should be made from WG (7 is best guess)
                        if (chronoEventList[nextID].startTime - WGBuildTime +0>= WGREndTime)
                        {
							//figure out how long unit should wait if WGR not done (but within 7 seconds)
							double timeWait = WGREndTime - (chronoEventList[nextID].startTime - WGBuildTime);
							if (timeWait < 0) { timeWait = 0; }
                            WGAdded = true;
                            //log this event as first WG event
                            firstWGEvents.Add(nextID);
							//shift first warpgate unit startTime back by 7 seconds (only if it isn't already shifted)
							//shift endTime by startTime + WGDuration
							//check if unit was previously not from WG
							if (!chronoEventList[nextID].fromWG)
							{
								WGIterationDone = false;
								chronoEventList[nextID].fromWG = true;
								chronoEventList[nextID].startTime = chronoEventList[nextID].startTime + 7 + timeWait;
								chronoEventList[nextID].endTime = chronoEventList[nextID].startTime + pairObjectLibrary[chronoEventList[nextID].bUUDataID].durationWithwarpGateResearch;
							}
                            lastID = nextID;
                        }
                        else
                        {
                            //check if this item is made out of warpgate but is before WGR is done (rare but is possible)
                            if (chronoEventList[nextID].fromWG)
                            {
                                int j = 0;
                                //remove this event from WG
                                //if this is the first WG unit, need to move startTime 
                            }
                        }
                    }
                    if (chronoEventList[nextID].pairAsParentID < 0) { break; }
                    nextID = originalPairList[chronoEventList[nextID].pairAsParentID].childBUUEventID;
                }
                //log lastID as final unit in this chain
                //make final unit have build time of 3.6 seconds
                if (WGAdded)
                {
                    chronoEventList[lastID].endTime = chronoEventList[lastID].startTime + 3.6;
                    double timeToShift = buildEndTime - chronoEventList[lastID].endTime;
                    //run back up chain and right shift everything to buildEndTime
                    for (int i = 0; i < 1000; i++)
                    {
                        chronoEventList[lastID].startTime = chronoEventList[lastID].startTime + timeToShift;
                        chronoEventList[lastID].endTime = chronoEventList[lastID].endTime + timeToShift;
                        lastID = originalPairList[chronoEventList[lastID].pairAsChildID].parentBUUEventID;
                        //check if lastID is gateway
                        if (pairObjectLibrary[chronoEventList[lastID].bUUDataID].isBuilding) { break; }
                    }
                    //shift if secondary building
                    if (thisGatewayUnit != gatewayRow.IDList[0])
                    {
                        chronoEventList[lastID].startTime = chronoEventList[lastID].startTime + timeToShift;
                        chronoEventList[lastID].endTime = chronoEventList[lastID].endTime + timeToShift;
                    }
                }
            }
            return firstWGEvents;
        }
        private void addWGEvents(List<PairsTable> chronoPairList, List<int> firstWGEvents)
        {
            compareBUUEvents newComparer = new compareBUUEvents();
            //first make chronoPairList an exact copy of pairList
            foreach (PairsTable thisPair in originalPairList)
            {
                PairsTable newPair = new PairsTable(thisPair.pairID, thisPair.parentBUUEventID, thisPair.childBUUEventID);
                chronoPairList.Add(newPair);
            }

            //add warpgates to chronoPairList
            //update child and parent pair names for pairs around WG

            //add warpgate to chronoEventList
            //use endTime at startTime of first warpgate event
            int WGBUUID = pairObjectLibrary.First(x => x.name.Equals("warpGate")).bUUDataID;
            foreach (int thisWGChild in firstWGEvents)
            {
                int pairIdOfWGAsChild = chronoPairList.Count(); //will be next element added
                int pairIdOfFirstWarpGateUnitAsChild = chronoEventList[thisWGChild].pairAsChildID;
                //add event
                int wgEventID = chronoEventList.Count();
                double endTime = chronoEventList[thisWGChild].startTime;
                double duration = pairObjectLibrary[WGBUUID].duration;
                bUUEvent newEvent = new bUUEvent(wgEventID, endTime - duration, endTime, WGBUUID, pairIdOfWGAsChild, pairIdOfFirstWarpGateUnitAsChild);
                chronoEventList.Add(newEvent);

                //also add event to semiOrderedChronoEventList (but put in correct location) ->use a binarysearch to find correct location
                //int index = semiOrderedChronoEventList.BinarySearch(newEvent, newComparer);
                //semiOrderedChronoEventList.Insert(index, newEvent);
                orderedChronoEventList.Add(newEvent);

                //add pair for warpgate as child
                int wgParentEventID = originalPairList[chronoEventList[thisWGChild].pairAsChildID].parentBUUEventID;
                PairsTable newPair = new PairsTable(pairIdOfWGAsChild, wgParentEventID, wgEventID);
                chronoPairList.Add(newPair);

                //edit pair with the first WGunit as child(its parent is now a WG)
                chronoPairList[pairIdOfFirstWarpGateUnitAsChild].parentBUUEventID = wgEventID;
            }
        }
        private void performCEChronos(double globalFastestBuildYet, List<PairsTable> chronoPairList, List<int> thisChronoOption, List<double> mineralStatements, List<double> gasStatements, int WGRSpot)
        {
            //if there is no WGR or no gateway units, chronoPairList will be empty
            int gatewayBUUDataID = pairObjectLibrary.First(x => x.name.Equals("gateway")).bUUDataID;
            bool includeGatewayUnits = lastBasketEntries.Where(x => x.ID == gatewayBUUDataID).Any();
            if (WGRSpot < 0 || !includeGatewayUnits) { chronoPairList = originalPairList; }
            //chrono details
            double chronoDuration = 20;
            double chronoEffect = 1.5;

            //log chrono details
            chronoCELog = new List<buildOrderEntry>();

            //assume that a nexus can hold infinite energy for now
            int numCEChronos = countNumCEChronos(thisChronoOption);
            if (numCEChronos == 0) { return; }

            //create a table of chain ends and number of chronos
            List<bUUEvent> childlessEvents = chronoEventList.Where(x => x.pairAsParentID < 0).ToList();
            List<int[]> chronoUsage = new List<int[]>();
            for (int i = childlessEvents.Count() - 1; i >= 0; i--)
            {
                if (!pairObjectLibrary[childlessEvents[i].bUUDataID].isBuilding)
                {
                    int[] newEntry = new int[2] { childlessEvents[i].bUUEventID, 0 };
                    chronoUsage.Add(newEntry);
                }
            }

            //before actually doing chronos, check if build has a chance of being fastest
            if (checkIfDiscardBasedOnCEChronos(numCEChronos, chronoUsage, globalFastestBuildYet)) { return; }

			//during each iteration the ordering of events may change (
			for (int i = 0; i < numCEChronos; i++)
            {

				//1. Order events by start time (already ordered)
				//List<bUUEvent> chronoOrderedList = new List<bUUEvent>(chronoEventList.OrderBy(x => x.startTime).ToList());
				//semiOrderedChronoEventList = semiOrderedChronoEventList.OrderBy(x => x.startTime).ToList();
				compareBUUEvents c = new compareBUUEvents();
                orderedChronoEventList.Sort(c);
                //2. Find TotalBuildCost for each Unit
                //3. Find Earliest Time afford for each unit
                //4. Find [timeProduced] - [timeAfford]
                int eventIDOfSmallestGap = findSmallestGap(orderedChronoEventList, mineralStatements, gasStatements);

                //apply chrono boost to end of the selected event chain
                int chainEndEventID = findChainEndEventID(eventIDOfSmallestGap, chronoPairList);

                int numChronosUsed = chronoUsage.First(x => x[0] == chainEndEventID)[1];
                double timeAlreadyChronoed = numChronosUsed * chronoDuration;
                //see how far back this chain is unit production (looking for at least (chronoDuration*chronoEffect)  
                int nextEvent = chainEndEventID;
                int lastEvent = 0;
                double chronoLeft = chronoDuration;
                double totalChainShift = 0;
                bool thisChronoLogged = false;
                double timeAlreadyChronoedIfThisLastUnit = 0;
                //loop this chrono back through as many units as neccessary
                for (int u = 0; u < 1000; u++)
                {
                    double unitDuration = chronoEventList[nextEvent].endTime - chronoEventList[nextEvent].startTime;
                    double unitDurationNotAlreadyChronoed = unitDuration - timeAlreadyChronoed;
                    if (unitDurationNotAlreadyChronoed < 0) { unitDurationNotAlreadyChronoed = 0; }

                    //add variable to track how much time this specific unit has already been chrono boosted, need to know if this last unit to be chronoed
                    timeAlreadyChronoedIfThisLastUnit = timeAlreadyChronoed;

                    timeAlreadyChronoed = timeAlreadyChronoed - unitDuration;
                    if (timeAlreadyChronoed < 0) { timeAlreadyChronoed = 0; }
                    //reduce last (chronoDuration*chronoEffect) seconds of this unit duration
                    double timeToReduce = unitDurationNotAlreadyChronoed * (1 - 1 / chronoEffect);
                    if (timeToReduce > chronoLeft * (chronoEffect - 1))
                    {
                        timeToReduce = chronoLeft * (chronoEffect - 1);
                    }

                    //shift timings
                    chronoEventList[nextEvent].startTime = chronoEventList[nextEvent].startTime + timeToReduce + totalChainShift;
                    chronoEventList[nextEvent].endTime = chronoEventList[nextEvent].endTime + totalChainShift;

                    //update totalChainShift for next element
                    totalChainShift = totalChainShift + timeToReduce;

                    //calculate how much chrono still available
                    chronoLeft = chronoLeft - timeToReduce / (chronoEffect - 1);

                    //if no chrono is left, that means chrono started on this unit. add event
                    if (chronoLeft == 0 && !thisChronoLogged)
                    {
                        thisChronoLogged = true;
                        //start chrono at unit end time - time already chronoed-20 seconds
                        double timeStartChrono = chronoEventList[nextEvent].endTime - timeToReduce / (chronoEffect - 1) - timeAlreadyChronoedIfThisLastUnit;
                        buildOrderEntry newEntry = new buildOrderEntry(pairObjectLibrary[chronoEventList[nextEvent].bUUDataID].name, timeStartChrono);
                        chronoCELog.Add(newEntry);
                    }

                    //find parent event
                    lastEvent = nextEvent;
                    nextEvent = chronoPairList[chronoEventList[nextEvent].pairAsChildID].parentBUUEventID;
                    //break if parentEvent is a building
                    if (pairObjectLibrary[chronoEventList[nextEvent].bUUDataID].isBuilding)
                    {
                        //check if chrono was never logged but at least partially used
                        if (chronoLeft > 0 && chronoLeft < chronoDuration)
                        {
                            //need to log chrono when unit starts
                            thisChronoLogged = true;
                            //start chrono at unit end time - time already chronoed-20 seconds
                            double timeStartChrono = chronoEventList[lastEvent].startTime;
                            buildOrderEntry newEntry = new buildOrderEntry(pairObjectLibrary[chronoEventList[lastEvent].bUUDataID].name, timeStartChrono);
                            chronoCELog.Add(newEntry);
                        }
                        break;
                    }
                }

                //add this tally to chronoUsage
                chronoUsage.First(x => x[0] == chainEndEventID)[1]++;

                //shift nextEvent (building) as far right as possible (up to totalChainShift)
                //also attempt to shift all primary events as possible
                rightShiftBuilding(nextEvent, chronoPairList);

                //left shift all nonWGR- components

                //find wiggleRoom if WG is included in build (wiggleRoom is the max amount items can be shifted left)
                double wiggleRoom = chronoEventList.Min(x => x.startTime);
                if (WGRSpot >= 0 && includeGatewayUnits)
                {
                    wiggleRoom = identifyWGRComponentsWiggleRoom(chronoEventList)-0.01;
                }
                //if wiggleRoom is bigger than an event start time, decrease wiggle room


                double timeBeforeShift = chronoEventList[chronoEventList.Count() - 1].endTime;
                //this item redorders list first because CE chrono changed that lists timing
                //creates an entire new list out of memory->might be better to have an existing orderedList that just gets pointed to the ordered version
                leftShiftEntireChronoBuild(wiggleRoom, mineralStatements, gasStatements);
                double timeShifted = timeBeforeShift - chronoEventList[chronoEventList.Count() - 1].endTime;
                //also left shift all logged chrono times
                chronoCELog = leftShiftChronoUsage(chronoCELog, timeShifted);
            }
        }
        private bool checkIfDiscardBasedOnCEChronos(int numCEChronos, List<int[]> unitChainEnds, double globalFastestBuildYet)
        {
            //each chronoBoost can at most reduce the build time by 10 seconds
            double buildEndTime = chronoEventList[unitChainEnds[0][0]].endTime;
            if (buildEndTime - 10 * numCEChronos > globalFastestBuildYet) { return true; }
            return false;
        }
        private int countNumCEChronos(List<int> thisChronoOption)
        {
            int numCEChronos = 0;
            //only consider chronos that are before endTime
            double buildEndTime = chronoEventList.Max(x => x.endTime);
            for (int i = 0; i < thisChronoOption.Count(); i++)
            {
                if (thisChronoOption[i] == 2 && approximateChronoTimes[i] < buildEndTime)
                {
                    numCEChronos++;
                }
            }
            return numCEChronos;
        }
        private int findSmallestGap(List<bUUEvent> chronoOrderedList, List<double> mineralStatements, List<double> gasStatements)
        {
            int eventIDOfSmallestGap = -1;
            double smallestGapLength = 100000;
            int totalMineralCost = 0;
            int totalGasCost = 0;
            double timeAfford = 0;
            double previousTimeAfford = 0;
            foreach (bUUEvent thisEvent in chronoOrderedList)
            {
                totalMineralCost = totalMineralCost + pairObjectLibrary[thisEvent.bUUDataID].mineralCost;
                totalGasCost = totalGasCost + pairObjectLibrary[thisEvent.bUUDataID].gasCost;
                //make sure not building
                //for now dont consider buildings (technically they should be considered while also considering precedence)
                if (!pairObjectLibrary[thisEvent.bUUDataID].isBuilding && !pairObjectLibrary[thisEvent.bUUDataID].name.Equals("warpGateResearch"))
                {
                    //pass in when previous event could be afforded as a minimum. outputs difference in time between last event and this event
                    timeAfford = findTimeUntilAfford(totalMineralCost, totalGasCost, previousTimeAfford, 0, mineralStatements, gasStatements);
                    double gapBottleneck = thisEvent.startTime - (timeAfford + previousTimeAfford);
                    //check if smallest gap yet
                    if (gapBottleneck < smallestGapLength)
                    {
                        smallestGapLength = gapBottleneck;
                        eventIDOfSmallestGap = thisEvent.bUUEventID;
                    }
                    //if equal use start time as tie breaker
                    if (gapBottleneck == smallestGapLength && chronoEventList[eventIDOfSmallestGap].startTime > thisEvent.startTime)
                    {
                        smallestGapLength = gapBottleneck;
                        eventIDOfSmallestGap = thisEvent.bUUEventID;
                    }
                    previousTimeAfford = previousTimeAfford + timeAfford;
                }

            }
            return eventIDOfSmallestGap;

        }
        private void rightShiftBuilding(int thisEvent, List<PairsTable> chronoPairList)
        {
            //may be a primary or secondary building.
            //if secondary, shift all the way to child startTime
            //if primary, consider start time of child as well as all precedence constraints
            //if primary its eventID will exist in precedence relations
            //if primary also attempt to move parents as possible
            if (precedenceRelations.Where(x => x.ID == thisEvent).Any())
            {
                //primaryEvent
                //need to right shift all buildings again
                //go through precedence relations in reverse order, 
                for (int i = precedenceRelations.Count() - 1; i >= 0; i--)
                {
                    double minEndTime = 100000;
                    int thisEventID = precedenceRelations[i].ID;
                    int thisBUUID = chronoEventList[thisEventID].bUUDataID;
                    //first check precedence items
                    foreach (int unitEventID in precedenceRelations[i].IDList)
                    {
						string name = chronoEventList[unitEventID].name;
						//check every item in chrono event list with this name (for isntance, if moving pylon to the right, need to check all gateways)
						foreach (bUUEvent item in chronoEventList)
						{
							if (item.name == name)
							{
								if (item.startTime < minEndTime)
								{
									minEndTime = item.startTime;
								}
							}
						}
                        
                    }
                    //next check last basket entry
                    List<IDWithList> lastUnit = lastBasketEntries.Where(x => x.ID == thisBUUID).ToList();
                    int lastEventID = -1;
                    if (lastUnit.Any())
                    {
                        //will only have 1 entry (check first item in IDList because this will be primary basket)
                        lastEventID = lastUnit[0].IDList[0];
                        if (chronoEventList[lastEventID].startTime < minEndTime)
                        {
                            minEndTime = chronoEventList[lastEventID].startTime;
                        }
                    }

                    //add event at minEndTime
                    double duration = pairObjectLibrary[chronoEventList[thisEventID].bUUDataID].duration;
                    chronoEventList[thisEventID].endTime = minEndTime;
                    chronoEventList[thisEventID].startTime = minEndTime - duration;
                }
            }
            else
            {
                //secondary event
                //shift event all the way to child 
                double childStartTime = chronoEventList[chronoPairList[chronoEventList[thisEvent].pairAsParentID].childBUUEventID].startTime;
                double amountToShift = childStartTime - chronoEventList[thisEvent].endTime;
                chronoEventList[thisEvent].startTime = chronoEventList[thisEvent].startTime + amountToShift;
                chronoEventList[thisEvent].endTime = chronoEventList[thisEvent].endTime + amountToShift;
            }

        }
        private int findChainEndEventID(int currentEventID, List<PairsTable> chronoPairList)
        {
            //go down chain until there is no child
            for (int i = 0; i < 1000; i++)
            {
                int pairAsParent = chronoEventList[currentEventID].pairAsParentID;
                if (pairAsParent < 0) { return currentEventID; }
                currentEventID = chronoPairList[pairAsParent].childBUUEventID;
            }
            return -1;
        }
        private List<buildOrderEntry> leftShiftChronoUsage(List<buildOrderEntry> chronoCELog, double timeShifted)
        {
            foreach (buildOrderEntry thisEntry in chronoCELog)
            {
                thisEntry.timeToBuild = thisEntry.timeToBuild - timeShifted;
            }
            return chronoCELog;
        }
    }
}
