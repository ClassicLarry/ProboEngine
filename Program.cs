using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
    class Program
    {
		static void Main(string[] args)
		{
			Random rand = new Random();
			//use the following two lines to generate random build order inputs for training
			//unitInputData myUnits = createRandomUnitInput(rand);
			//upgradeInputData myUpgrades = createRandomUpgradeInput(rand);

			//use the following two lines to generate specific build order entries (currently set to 23 glaive adepts)
			unitInputData myUnits = new unitInputData(0, 0, 0, 23, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
			upgradeInputData myUpgrades = new upgradeInputData(false, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false,
			false, false, true, false, false, false, false, false, false, false);
			List<string[]> finalBuildOrder = timeChecker(myUnits, myUpgrades);
		}
		private static unitInputData createRandomUnitInput(Random rand)
		{
			int unitBag = rand.Next(3);
			int numUnits;
			if (unitBag == 0)
			{
				numUnits = rand.Next(30) + 1;
			}
			else if (unitBag == 1)
			{
				numUnits = rand.Next(15) + 1;
			}
			else
			{
				numUnits = rand.Next(5) + 1;
			}
			//split units amongst type
			int[] unitArray = new int[16];

			//perform for loop until all units are sorted
			for (int x = 0; x < 50; x++)
			{
				int fractionSplit = rand.Next(8) + 1;
				int unitsToAdd = (int)Math.Ceiling((double)numUnits / (double)fractionSplit);
				//add to a random split
				int spotToAdd = rand.Next(16);
				unitArray[spotToAdd] = unitArray[spotToAdd] + unitsToAdd;
				numUnits = numUnits - unitsToAdd;
				if (numUnits == 0) { break; }
			}
			unitInputData newData = new unitInputData(unitArray[0], unitArray[1], unitArray[2], unitArray[3], unitArray[4], unitArray[5], unitArray[6], unitArray[7], unitArray[8], unitArray[9]
				, unitArray[10], unitArray[11], unitArray[12], unitArray[13], unitArray[14], unitArray[15]);
			return newData;
		}
		private static upgradeInputData createRandomUpgradeInput(Random rand)
		{
			//15 tiered upgrades
			//warpgate
			//10 other upgrades
			//give each tier a random #0-15 (7,8,9 correspond to upgrades)
			//give warpgate 3/4 chance of included
			//give 10 other upgrades each .1 chance 
			bool[] upgrades = new bool[26];
			for (int i = 0; i < 3; i++)
			{
				int extra = 0; //account for warpgate being 10th upgrade
				if (i > 2) { extra = 1; }
				int newNum = rand.Next(15);
				if (newNum == 12)//+1
				{
					upgrades[i * 3 + extra] = true;
				}

			}
			int wGChance = rand.Next(4);
			if (wGChance > 0) { upgrades[9] = true; }

			//go through last 10 upgrades
			for (int x = 0; x < 10; x++)
			{
				int chance = rand.Next(20);
				if (chance == 0) { upgrades[16 + x] = true; }
			}
			upgradeInputData newData = new upgradeInputData(upgrades[0], upgrades[1], upgrades[2], upgrades[3], upgrades[4], upgrades[5], upgrades[6], upgrades[7], upgrades[8], upgrades[9],
				upgrades[10], upgrades[11], upgrades[12], upgrades[13], upgrades[14], upgrades[15], upgrades[16], upgrades[17], upgrades[18], upgrades[19], upgrades[20], upgrades[21],
				upgrades[22], upgrades[23], upgrades[24], upgrades[25]);
			return newData;

		}
		public static List<string[]> timeChecker(unitInputData newUnitInputData, upgradeInputData newUpgradeInputData)
		{
			try
			{
				var task = Task.Run(() =>
				{
					return IterationManager(newUnitInputData, newUpgradeInputData);
				});
				//set time limit excessively large for now
				bool isCompletedSuccessfully = task.Wait(TimeSpan.FromMilliseconds(20000000));

				if (isCompletedSuccessfully)
				{
					return task.Result;
				}
				else
				{
					throw new TimeoutException("The function has taken longer than the maximum time allowed.");
				}
			}
			catch
			{
				List<string[]> newList = new List<string[]>();
				return newList;
			}

		}
		public static List<string[]> IterationManager(unitInputData newUnitInputData, upgradeInputData newUpgradeInputData)
		{
			//MAIN FUNCTION FOR GENERATING BUILD ORDER
			//initialize buildings and unit objects based on unit and upgrade demands
			buildCreatorPreEcon thisBuild = new buildCreatorPreEcon(newUnitInputData, newUpgradeInputData);

			//perform one units-economy iteration for each starting build option
			List<dynamicEconBuildRunnerClass> buildsFromDiffEcos = new List<dynamicEconBuildRunnerClass>();
			//iterate through six different approximations of how economy may develop
			for (int ecoOption = 0; ecoOption < 6; ecoOption++)
			{
				//generate estimate of chronoboost and economy development for this option (array of how many minerals and gas are expected to be available at each second)
				List<BankStatement> approximateFreeCashFunctionL1 = createApproximateFreeCashFunction(ecoOption);
				List<double> approximateChronoTimesL1 = createApproximateChronoTimes(); //based on only having 1 nexi

				//based on economy projection, solve for ordering of building and unit production
				List<buildOrderEntry> compressedBuild = thisBuild.createCompressedBuilds(approximateFreeCashFunctionL1, approximateChronoTimesL1, 10000000, 0, false);

				//only create buildIteration if viable result was found
				if (compressedBuild[0].itemToBuild != "Dummy")
				{
					//optimize economic portion of build (Probes, Pylons, Assimilators, Nexi) based on when units were produced
					dynamicEconBuildRunnerClass buildIteration = optimizeEconomicVariables(thisBuild, compressedBuild);
					buildsFromDiffEcos.Add(buildIteration);
				}

			}
			//Repeat iterative process above based on best performing option of the six

			//find economy and chronos from fastest option, and then iterate with that
			double fastestRun = buildsFromDiffEcos.Min(x => x.endTime);
			int fastestIteration = buildsFromDiffEcos.IndexOf(buildsFromDiffEcos.First(x => x.endTime == fastestRun));
			dynamicEconBuildRunnerClass fastestBuildIteration = buildsFromDiffEcos[fastestIteration];
			//create new mineral function and chrono list from fastest iteration
			//find free cash function
			List<BankStatement> approximateFreeCashFunction = findFreeCashFunction(fastestBuildIteration);
			//update approximateChronoTimes
			List<double> approximateChronoTimes = fastestBuildIteration.timeChronoAvailable;

			//loop process of creating BUU based on freeCashFunction, optimizing economic coefficients, and finding new freeCashFunction
			List<dynamicEconBuildRunnerClass> iterationResults = new List<dynamicEconBuildRunnerClass>();
			double fastestYet = 10000000;
			double forLogFastestYet = 100000;
			bool chronoFirstProbe = false;
			//check if first Probe was chronoed
			if (fastestBuildIteration.chronoEcoLog.Any()) { chronoFirstProbe = true; }
			List<buildOrderEntry> fastestSlimBuild = new List<buildOrderEntry>();
			//run a maximum of 4 iterations
			for (int i = 1; i < 4; i++)
			{
				List<buildOrderEntry> compressedBuild = thisBuild.createCompressedBuilds(approximateFreeCashFunction, approximateChronoTimes, fastestYet, i, chronoFirstProbe);
				//allBuildsCatalog.Add(allCompressedBuildOrders[0]);

				dynamicEconBuildRunnerClass buildIteration = optimizeEconomicVariables(thisBuild, compressedBuild);
				if (buildIteration.endTime < forLogFastestYet)
				{
					forLogFastestYet = buildIteration.endTime;
					fastestSlimBuild = new List<buildOrderEntry>(compressedBuild);
				}
				iterationResults.Add(buildIteration);

				//check if first Probe was chronoed
				if (buildIteration.chronoEcoLog.Any()) { chronoFirstProbe = true; }

				//exit if iteration has converged (result is same as last result)
				if (iterationResults.Count() > 1)
				{
					if (iterationResults[i - 1].endTime == iterationResults[i - 2].endTime)
					{
						break;
					}
				}

				//find free cash function
				approximateFreeCashFunction = findFreeCashFunction(buildIteration);
				//update approximateChronoTimes
				approximateChronoTimes = buildIteration.timeChronoAvailable;
			}


			double finalFastestRun = iterationResults.Min(x => x.endTime);
			int finalFastestIteration = iterationResults.IndexOf(iterationResults.First(x => x.endTime == finalFastestRun));

			//run through fastest item again and log decision + state to database
			double[] dummy = new double[1];
			coefficientPackage dummy2 = new coefficientPackage(dummy, dummy, dummy, dummy, dummy);
			List<buildOrderEntry> dummyList = new List<buildOrderEntry>();

			//build runner needs to have selected decision and buildOrderList from original attempt
			dynamicEconBuildRunnerClass testBuild = new dynamicEconBuildRunnerClass(thisBuild, iterationResults[finalFastestIteration].originalNoEconBuild, dummy2, false, false, false, iterationResults[finalFastestIteration].realizedBuildLog);

			//after looping, pull final results from buildIteration
			//add chronos to build log
			addChronoToLog(iterationResults[finalFastestIteration]);
			string[] nameOutputs = convertToStringArray(iterationResults[finalFastestIteration].realizedBuildLog, true);
			string[] timeOutputs = convertToStringArray(iterationResults[finalFastestIteration].realizedBuildLog, false);
			string[] supplyOutputs = buildSupplyArray(iterationResults[finalFastestIteration]);
			string[] generalInfo = buildGenInfo(iterationResults[finalFastestIteration]);
			timeOutputs = fixFormat(timeOutputs);

			List<string[]> output = new List<string[]> { nameOutputs, timeOutputs, supplyOutputs, generalInfo };
			return output;
		}
		public static string[] convertToStringArray(List<buildOrderEntry> buildOrder, bool returnNames)
		{
			string[] listToReturn = new string[buildOrder.Count()];
			if (returnNames)
			{
				for (int x = 0; x < buildOrder.Count(); x++)
				{
					string item = buildOrder[x].itemToBuild;
					string last2Item = item.Substring(item.Length - 2);
					int n;
					if (int.TryParse(last2Item, out n))
					{
						listToReturn[x] = item.Substring(0, item.Length - 2) + " " + last2Item;
					}
					else
					{
						string lastItem = item.Substring(item.Length - 1);
						if (int.TryParse(lastItem, out n))
						{
							listToReturn[x] = item.Substring(0, item.Length - 1) + " " + lastItem;
						}
						else
						{
							listToReturn[x] = buildOrder[x].itemToBuild;
						}

					}
				}
			}
			else
			{
				for (int x = 0; x < buildOrder.Count(); x++)
				{
					listToReturn[x] = buildOrder[x].timeToBuild.ToString();
				}
			}
			return listToReturn;
		}
		private static string[] buildGenInfo(dynamicEconBuildRunnerClass thisBuild)
		{
			string[] output = new string[5];
			int minutes = (int)thisBuild.endTime / 60;
			int seconds = (int)thisBuild.endTime - 60 * minutes;
			string secondsString = seconds.ToString();
			if (seconds < 10) { secondsString = "0" + secondsString; }
			output[0] = minutes.ToString() + ":" + secondsString;
			output[1] = thisBuild.currentSupply.ToString();
			output[2] = thisBuild.currentSupplyCap.ToString();
			output[3] = "12"; //numProbes
			output[4] = "1"; //numNexus
			if (thisBuild.realizedBuildLog.Where(x => x.itemToBuild == "Probe").Any())
			{
				output[3] = (12 + thisBuild.realizedBuildLog.Where(x => x.itemToBuild == "Probe").Count()).ToString();
			}
			if (thisBuild.realizedBuildLog.Where(x => x.itemToBuild == "nexus").Any())
			{
				output[4] = (1 + thisBuild.realizedBuildLog.Where(x => x.itemToBuild == "nexus").Count()).ToString();
			}
			return output;
		}
		private static string[] fixFormat(string[] timeOutputs)
		{
			for (int i = 0; i < timeOutputs.Count(); i++)
			{
				double time = Convert.ToDouble(timeOutputs[i]);
				int mins = (int)time / 60;
				int seconds = (int)time - 60 * mins;
				string secondsString;
				if (seconds < 10) { secondsString = "0" + seconds.ToString(); } else { secondsString = seconds.ToString(); }
				timeOutputs[i] = mins.ToString() + ":" + secondsString;
			}
			return timeOutputs;
		}
		private static void addChronoToLog(dynamicEconBuildRunnerClass buildDetails)
		{
			//go through each chrono event and add to list
			if (buildDetails.chronoCELog != null)
			{
				for (int a = 0; a < buildDetails.chronoCELog.Count(); a++)
				{
					//build item
					buildOrderEntry newEntry = new buildOrderEntry("CHRONOBOOST " + buildDetails.chronoCELog[a].itemToBuild, buildDetails.chronoCELog[a].timeToBuild);
					//find where to add,loop through whole list
					for (int x = 0; x < buildDetails.realizedBuildLog.Count(); x++)
					{
						if (newEntry.timeToBuild < buildDetails.realizedBuildLog[x].timeToBuild)
						{
							buildDetails.realizedBuildLog.Insert(x, newEntry);
							break;
						}
						else if (x == buildDetails.realizedBuildLog.Count() - 1)
						{
							buildDetails.realizedBuildLog.Add(newEntry);
							break;
						}
					}
				}
			}
			if (buildDetails.chronoWGRLog != null)
			{
				//make sure WGR isn't chronoboosted before its started
				double WGRStartTime = buildDetails.realizedBuildLog.First(x => x.itemToBuild == "warpGateResearch").timeToBuild;
				//build item
				double shift = 0;
				if (buildDetails.chronoWGRLog.Count() > 0)
				{
					shift = WGRStartTime - buildDetails.chronoWGRLog[0].timeToBuild + 0.01;
				}
				for (int a = 0; a < buildDetails.chronoWGRLog.Count(); a++)
				{
					buildOrderEntry newEntry = new buildOrderEntry("CHRONOBOOST " + buildDetails.chronoWGRLog[a].itemToBuild, buildDetails.chronoWGRLog[a].timeToBuild + shift);
					//find where to add,loop through whole list
					for (int x = 0; x < buildDetails.realizedBuildLog.Count(); x++)
					{
						if (newEntry.timeToBuild < buildDetails.realizedBuildLog[x].timeToBuild)
						{
							buildDetails.realizedBuildLog.Insert(x, newEntry);
							break;
						}
						else if (x == buildDetails.realizedBuildLog.Count() - 1)
						{
							buildDetails.realizedBuildLog.Add(newEntry);
							break;
						}
					}
				}
			}
			if (buildDetails.chronoEcoLog != null)
			{
				for (int a = 0; a < buildDetails.chronoEcoLog.Count(); a++)
				{
					//build item
					buildOrderEntry newEntry = new buildOrderEntry("CHRONOBOOST " + buildDetails.chronoEcoLog[a].itemToBuild, buildDetails.chronoEcoLog[a].timeToBuild);
					//find where to add,loop through whole list
					for (int x = 0; x < buildDetails.realizedBuildLog.Count(); x++)
					{
						if (newEntry.timeToBuild < buildDetails.realizedBuildLog[x].timeToBuild)
						{
							buildDetails.realizedBuildLog.Insert(x, newEntry);
							break;
						}
						else if (x == buildDetails.realizedBuildLog.Count() - 1)
						{
							buildDetails.realizedBuildLog.Add(newEntry);
							break;
						}
					}
				}
			}

		}
		private static string[] buildSupplyArray(dynamicEconBuildRunnerClass fastestClass)
		{
			int runningSupply = 12;
			string[] output = new string[fastestClass.realizedBuildLog.Count()];
			output[0] = runningSupply.ToString();
			for (int i = 1; i < fastestClass.realizedBuildLog.Count(); i++)
			{
				int supplyToAdd = 0;
				string name = fastestClass.realizedBuildLog[i - 1].itemToBuild;
				if (name == "Probe") { supplyToAdd = 1; }
				else if (fastestClass.SlimBuild.Where(x => x.name == name).Any())
				{
					supplyToAdd = fastestClass.SlimBuild.First(x => x.name == name).supplyCost;
				}
				runningSupply = supplyToAdd + runningSupply;
				output[i] = runningSupply.ToString();
			}
			return output;
		}
		private static List<bUUDataObject> createFullPairObjectLibrary()
		{
			unitInputData newUnitInputData = new unitInputData(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
			upgradeInputData newUpgradeInputData = new upgradeInputData(true, true, true, true, true, true, true, true, true, true,
				true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true);
			//create barebones building and unit portion of build order
			buildCreatorPreEcon thisBuild = new buildCreatorPreEcon(newUnitInputData, newUpgradeInputData);
			return thisBuild.pairObjectsLibrary;
		}
		private static List<double> createApproximateChronoTimes()
		{
			List<double> chronoTimes = new List<double>();
			//chrono boost costs 50 energy, and a nexus regains energy at .7875 energy/s
			//nexus starts with 50 energy
			int startingEnergy = 50;
			double regenRate = 0.7875;
			int costToChrono = 50;
			//initialize chrono boost times for first nexus
			//do all times up to 500 seconds
			for (int i = 0; i < 500; i++)
			{
				double newTime = i * costToChrono / regenRate;
				chronoTimes.Add(newTime);
				if (newTime > 500) { break; }
			}
			return chronoTimes;
		}
		private static dynamicEconBuildRunnerClass optimizeEconomicVariables(buildCreatorPreEcon thisBuild, List<buildOrderEntry> thisCompressedBuild)
		{
			//specify parameters
			//MOST/ALL of THESE PARAMETERS ARE NO LONGER USED
			//probe parameters
			double[] prVals = new double[6];
			prVals[0] = 1;
			prVals[1] = 1;
			prVals[2] = 2;
			prVals[3] = 12;
			prVals[4] = 4;
			prVals[5] = 2;

			//assimilator parameters
			double[] aVals = new double[7];
			aVals[0] = 100;
			aVals[1] = 1;
			aVals[2] = 0;
			aVals[3] = .3;
			aVals[4] = .5;
			aVals[5] = .1;
			aVals[6] = 2;

			//event parameters
			double[] eVals = new double[2];
			eVals[0] = 8;
			eVals[1] = 1;

			//pylon parameters
			double[] pyVals = new double[2];
			pyVals[0] = Math.Pow(10, 100);
			pyVals[1] = 1;

			//nexus parameters
			double[] nVals = new double[11];
			nVals[0] = .005;
			nVals[1] = 1;
			nVals[2] = 24;
			nVals[3] = 2;
			nVals[4] = 10;
			nVals[5] = 10;
			nVals[6] = 1;
			nVals[7] = .5;
			nVals[8] = 10;
			nVals[9] = 60;
			nVals[10] = 1;

			//put into coefficient package
			coefficientPackage thisCoefficientPackage = new coefficientPackage(prVals, nVals, eVals, aVals, pyVals);
			List<buildOrderEntry> bestBuild = new List<buildOrderEntry>();
			//reset coeffcients
			thisCoefficientPackage = new coefficientPackage(prVals, nVals, eVals, aVals, pyVals);

			//optimize general parameters
			int bestVal = 0;

			thisCoefficientPackage.aVals[0] = 1; //general Assim
			thisCoefficientPackage.aVals[1] = 1; //bias term on cost of gas units
			thisCoefficientPackage.aVals[2] = 0; //how long until more assimilators will be started (1->5 seconds, 0->100 seconds)
			thisCoefficientPackage.nVals[0] = 1; //general Nexus
			thisCoefficientPackage.nVals[1] = 1; //6seconds vs 12seconds for more probes on average (1 vs 0)
			thisCoefficientPackage.nVals[2] = 1; //4 vs 16 probes at new nexus (1 vs 0)
			thisCoefficientPackage.nVals[3] = 1; //gasIncomeNow based on maxGasIncome on number of bases vs 0.8 weighting on currentGasIncome (1 vs0)
			thisCoefficientPackage.nVals[4] = 1; //how much hurting event time is weighted in decision (0.5,2)
			thisCoefficientPackage.prVals[0] = 1; //probe general term
			thisCoefficientPackage.eVals[0] = 1; //event general term

			//first get runTime from Neural Network
			dynamicEconBuildRunnerClass testBuild = new dynamicEconBuildRunnerClass(thisBuild, thisCompressedBuild, thisCoefficientPackage, false, false, true);
			dynamicEconBuildRunnerClass localMinBuildFromNN = performLocalPertubation(testBuild.realizedBuildLog, thisBuild, thisCompressedBuild, thisCoefficientPackage, 100000);
			double nnEndTime = localMinBuildFromNN.endTime;
			dynamicEconBuildRunnerClass firstRun = new dynamicEconBuildRunnerClass(thisBuild, thisCompressedBuild, thisCoefficientPackage, false);
			double bestTime = dynamicEconSingleBuildRunnerAdmin(thisBuild, thisCompressedBuild, thisCoefficientPackage).endTime;
			//coefficient run:
			List<double> zeroToOne = new List<double> { 0, 0.2, 0.4, 0.6, 0.8, 1 };
			List<double> pointFiveToTwo = new List<double> { 0.5, 0.7, 0.9, 1, 1.2, 1.5, 1.8, 2 };
			//vary aVals[0], nVals[0], prVals[0], eVals[0], nVals[4]
			//vary nVals[1,2,3] aVals[1,2]
			//9 items to vary, each with ~3 possibilities. = 3^9 possibilities
			//perform 44 iterations. cascade through each option twice, picking best of 3 choices
			bestTime = basicCoefOptimizer(pointFiveToTwo, "e", 0, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			//dont let nexus go to zero before trying to optimize entry point
			bestTime = basicCoefOptimizer(pointFiveToTwo, "n", 0, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			bestTime = basicCoefOptimizer(pointFiveToTwo, "n", 4, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			bestTime = basicCoefOptimizer(pointFiveToTwo, "a", 0, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			bestTime = basicCoefOptimizer(pointFiveToTwo, "pr", 0, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);

			bestTime = basicCoefOptimizer(zeroToOne, "n", 1, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			bestTime = basicCoefOptimizer(pointFiveToTwo, "a", 1, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			bestTime = basicCoefOptimizer(zeroToOne, "n", 2, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			bestTime = basicCoefOptimizer(zeroToOne, "a", 2, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			bestTime = basicCoefOptimizer(zeroToOne, "n", 4, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);

			bestTime = basicCoefOptimizer(zeroToOne, "n", 0, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			List<buildOrderEntry> newBest = dynamicEconSingleBuildRunnerAdmin(thisBuild, thisCompressedBuild, thisCoefficientPackage).buildOrder;
			//perform local pertubation on build result
			dynamicEconBuildRunnerClass localMinBuild = performLocalPertubation(newBest, thisBuild, thisCompressedBuild, thisCoefficientPackage, bestTime);
			//return fastest of neuralnetwork approach and holistic search
			if (localMinBuild.endTime < nnEndTime)
			{ return localMinBuild; }
			else { return localMinBuildFromNN; }
		}
		private static dynamicEconBuildRunnerClass performLocalPertubation(List<buildOrderEntry> originalBuild, buildCreatorPreEcon thisBuild, List<buildOrderEntry> thisCompressedBuild, coefficientPackage thisCoefficientPackage, double bestTime)
		{
			//swap ordering of econ events in build doing local moves
			//select all pylons, assimilators, and nexi in build, and move locally
			//first optimize pylon timing
			//start at begining of build and try to move each assimilator later
			List<buildOrderEntry> bestBuildYet = new List<buildOrderEntry>(originalBuild);
			List<buildOrderEntry> nextTestBuild = new List<buildOrderEntry>(originalBuild);
			List<string> econItems = new List<string> { "Probe", "pylon", "nexus", "assimilator" };
			//perform 1000 perturbations
			//randomly pick spot in build, check if econ item
			Random rand = new Random(42);
			int nextSpot = 0;
			int amountMove = 0;
			int[] moveOptions = new int[] { -1, 1 };
			double newEndTime = 0;
			int numRuns = 0;
			int lastSuccessfulRun = 0;

			for (int i = 0; i < 10000; i++)
			{
				if (numRuns - 100 > lastSuccessfulRun) { break; }
				nextSpot = rand.Next(originalBuild.Count());

				if (econItems.Contains(bestBuildYet[nextSpot].itemToBuild))
				{
					//randomly move forward or backwards 1 spot
					amountMove = moveOptions[rand.Next(2)];
					//first make sure spots have different items
					if (nextSpot + amountMove < 0) { amountMove = 1; }
					if (nextSpot + amountMove >= bestBuildYet.Count()) { amountMove = -1; }

					//dont let first pylon change spots with building after it
					//first pylon will always be in first ~7 spots
					if (nextTestBuild[nextSpot].itemToBuild != bestBuildYet[nextSpot + amountMove].itemToBuild && !(bestBuildYet[nextSpot].itemToBuild == "pylon" && nextSpot < 7 && bestBuildYet[nextSpot + amountMove].itemToBuild == "gateway"))
					{
						nextTestBuild[nextSpot + amountMove] = bestBuildYet[nextSpot];
						nextTestBuild[nextSpot] = bestBuildYet[nextSpot + amountMove];

						dynamicEconBuildRunnerClass testBuild = new dynamicEconBuildRunnerClass(thisBuild, thisCompressedBuild, thisCoefficientPackage, false, false, false, nextTestBuild);
						numRuns++;
						newEndTime = testBuild.endTime;

						//compare endTimes
						if (bestTime > newEndTime)
						{
							lastSuccessfulRun = numRuns;
							//replace bestBuildYet and bestTime
							bestBuildYet[nextSpot] = nextTestBuild[nextSpot];
							bestBuildYet[nextSpot + amountMove] = nextTestBuild[nextSpot + amountMove];
							bestTime = newEndTime;
						}
						else
						{
							//revert change made to nextTestBuild
							nextTestBuild[nextSpot + amountMove] = bestBuildYet[nextSpot + amountMove];
							nextTestBuild[nextSpot] = bestBuildYet[nextSpot];
						}
					}
				}
			}
			//now try removing probes, starting with last probe made
			//for (int i = bestBuildYet.Count() - 1; i >= 0; i--)
			//{
			//	if (bestBuildYet[i].itemToBuild == "Probe")
			//	{
			//		//verify nextTestBUild is also probe
			//		if (nextTestBuild[i].itemToBuild != "Probe")
			//		{
			//			int stop = 0;
			//		}
			//		//try removing this probe
			//		nextTestBuild.RemoveAt(i);
			//		dynamicEconBuildRunnerClass testBuild = new dynamicEconBuildRunnerClass(thisBuild, thisCompressedBuild, thisCoefficientPackage, false, false, nextTestBuild);
			//		newEndTime = testBuild.endTime;
			//		if (bestTime > newEndTime)
			//		{
			//			//remove same item from best build, reduce count by an extra item to account for removing that item

			//			bestTime = newEndTime;
			//			bestBuildYet.RemoveAt(i);
			//			i--;
			//			if (i == 0) { break; }
			//		}
			//		else
			//		{
			//			//if removing last probe didn't help, exit
			//			buildOrderEntry newEntry = new buildOrderEntry("Probe", 0);
			//			nextTestBuild.Insert(i, newEntry);
			//			break;
			//		}
			//	}
			//}
			//try moving each pylon backwards a spot
			bool firstPylonChecked = false;
			for (int i = 0; i < bestBuildYet.Count() - 1; i++)
			{
				if (bestBuildYet[i].itemToBuild == "pylon")
				{
					if (firstPylonChecked)
					{
						//try moving this pylon one spot backwards
						nextTestBuild[i + 1] = bestBuildYet[i];
						nextTestBuild[i] = bestBuildYet[i + 1];

						dynamicEconBuildRunnerClass testBuild = new dynamicEconBuildRunnerClass(thisBuild, thisCompressedBuild, thisCoefficientPackage, false, false, false, nextTestBuild);
						newEndTime = testBuild.endTime;

						//compare endTimes
						if (bestTime >= newEndTime)
						{
							//replace bestBuildYet and bestTime
							bestBuildYet[i] = nextTestBuild[i];
							bestBuildYet[i + 1] = nextTestBuild[i + 1];
							bestTime = newEndTime;
						}
						else
						{
							//revert change made to nextTestBuild
							nextTestBuild[i + 1] = bestBuildYet[i + 1];
							nextTestBuild[i] = bestBuildYet[i];
						}
					}
					else { firstPylonChecked = true; }
				}
			}


			dynamicEconBuildRunnerClass testBuild2 = new dynamicEconBuildRunnerClass(thisBuild, thisCompressedBuild, thisCoefficientPackage, true, false, false, bestBuildYet);
			return testBuild2;
		}
		private static double basicCoefOptimizer(List<double> oldValsToTry, string groupName, int spot, buildCreatorPreEcon thisBuild, List<buildOrderEntry> thisCompressedBuildOrder, coefficientPackage thisCoefficientPackage, double bestTime)
		{

			double[] refList = thisCoefficientPackage.eVals;
			if (groupName == "pr")
			{
				refList = thisCoefficientPackage.prVals;
			}
			else if (groupName == "a") { refList = thisCoefficientPackage.aVals; }
			else if (groupName == "e") { refList = thisCoefficientPackage.eVals; }
			else if (groupName == "n") { refList = thisCoefficientPackage.nVals; }

			//find which value refList is already using
			List<double> valsToTry = new List<double>(oldValsToTry);
			valsToTry = valsToTry.Where(x => x != refList[spot]).ToList();
			double bestVal = refList[spot];
			for (int i = 0; i < valsToTry.Count(); i++)
			{
				//run two variations that haven't been run, and save coef to best value of the three
				refList[spot] = valsToTry[i];
				buildOrderWithEndTime buildResult = dynamicEconSingleBuildRunnerAdmin(thisBuild, thisCompressedBuildOrder, thisCoefficientPackage);
				if (buildResult.endTime < bestTime) { bestVal = valsToTry[i]; bestTime = buildResult.endTime; }
			}
			refList[spot] = bestVal;
			return bestTime;

		}
		private static List<BankStatement> createApproximateFreeCashFunction(int ecoOption)
		{
			if (ecoOption == 0)
			{
				List<BankStatement> approxFunction = new List<BankStatement>();
				for (int t = 0; t < 500; t++)
				{
					double minerals = 8 * t + Math.Pow(t, 2) / 24;
					//alow mineral slop to max at 3 base economy
					//~40 minerals a second
					if (minerals > t * 40) { minerals = t * 40; }
					double gas = 0;
					if (t > 100)
					{
						//allow slope to reach equivilant of 6 gas geysers (~24 gas/s)
						gas = (t - 100) * 4 + Math.Pow(t, 1.3);
						if (gas > 24 * (t - 100)) { gas = 24 * (t - 100); }
					}
					BankStatement newStatement = new BankStatement(minerals, gas);
					approxFunction.Add(newStatement);
				}
				return approxFunction;
			}
			else if (ecoOption == 1)
			{
				List<BankStatement> approxFunction = new List<BankStatement>();
				//have initial function go on for 500 seconds 
				//mimic 1 base progression
				List<double> first100MineralCounts = new List<double> { 0,0,0,0,11.4996,22.9992,34.4988,45.9984,57.498,68.9976,68.9976
				,68.9976,68.9976,68.9976,78.4122,90.8701,103.328,115.7859,128.2438,140.7017,153.1596,165.6175,165.6175
				,165.6175,165.6175,166.4074,179.8236,193.2398,206.656,220.0722,233.4884,246.9046,260.3208,273.737,273.737
				,273.737,273.737,273.737,273.737,273.737,273.737,273.737,273.737,273.737,273.737,273.737,284.6472,299.98
				,315.3128,315.3128,315.3128,315.3128,315.3128,315.3128,315.3128,315.3128,316.8084,333.0995,349.3906,365.6817
				,381.9728,381.9728,381.9728,381.9728,399.0538,416.3032,433.5526,450.802,468.0514,482.4259,496.8004,511.1749
				,511.1749,511.1749,511.1749,511.1749,534.964,534.964,534.964,534.964,534.964,534.964,534.964,534.964,534.964
				,534.964,534.964,534.964,547.875,561.2912,574.7074,588.1236,601.5398,614.956,628.3722,641.7884,641.7884
				,641.7884,641.7884,641.7884,660.786};
				for (int t = 0; t < 500; t++)
				{
					double minerals;
					double gas;
					//set mineral amount
					if (t < 100)
					{
						minerals = first100MineralCounts[t];
					}
					else { minerals = first100MineralCounts[99] + (t - 99) * 15.33; }

					//set gas amount
					//assume geysers done at 68 and 80
					if (t < 68) { gas = 0; }
					else if (t < 80) { gas = (t - 67) * 3 * 0.94228; }
					else { gas = (t - 79) * 6 * 0.94228 + (80 - 67) * 3 * 0.94228; }
					BankStatement newStatement = new BankStatement(minerals, gas);
					approxFunction.Add(newStatement);
				}
				return approxFunction;
			}
			else if (ecoOption == 2)
			{
				List<BankStatement> approxFunction = new List<BankStatement>();
				//have initial function go on for 500 seconds 
				//mimic 1gate fe with 2 gas total progression
				List<double> first244MineralCounts = new List<double> { 0,0,0,0,11.4996,22.9992,34.4988,45.9984,53.4964,53.4964
					,53.4964,53.4964,53.4964,65.9543,78.4122,90.8701,103.328,115.7859,128.2438,140.7017,152.9912,152.9912
					,152.9912,152.9912,152.9912,166.4074,179.8236,193.2398,206.656,210.3208,210.3208,210.3208,210.3208
					,223.737,237.1532,250.5694,263.9856,271.4836,271.4836,271.4836,271.4836,285.8581,295.9784,295.9784
					,295.9784,295.9784,295.9784,295.9784,295.9784,295.9784,295.9784,311.3112,313.4604,313.4604,313.4604
					,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604
					,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604
					,313.4604,313.4604,313.4604,313.4604,313.4604,313.4604,328.7932,344.126,359.4588,374.7916,390.1244
					,405.4572,420.79,436.1228,451.4556,466.7884,482.1212,496.3273,496.3273,496.3273,496.3273,512.6184
					,519.0739,519.0739,519.0739,519.0739,519.0739,535.365,551.6561,553.0675,553.0675,553.0675,553.0675
					,553.0675,553.0675,553.0675,553.0675,553.0675,570.3169,587.5663,604.8157,622.0651,623.8133,623.8133
					,623.8133,642.021,660.2287,678.4364,696.6441,714.8518,730.1846,745.5174,760.8502,772.1814,772.1814
					,772.1814,772.1814,788.4725,804.7636,821.0547,837.3458,853.6369,869.928,886.2191,902.5102,917.6746
					,917.6746,917.6746,917.6746,934.924,952.1734,953.9216,953.9216,953.9216,971.171,988.4204,1005.6698
					,1022.9192,1024.6674,1024.6674,1024.6674,1042.8751,1061.0828,1065.7059,1065.7059,1065.7059,1084.8719
					,1104.0379,1123.2039,1142.3699,1149.8679,1149.8679,1149.8679,1169.9922,1190.1165,1200.4894,1200.4894
					,1200.4894,1221.572,1242.6546,1263.7372,1284.8198,1298.0676,1298.0676,1298.0676,1320.1085,1342.1494
					,1364.1903,1386.2312,1408.2721,1427.2697,1427.2697,1427.2697,1450.2689,1473.2681,1496.2673,1519.2665
					,1543.224,1565.0965,1565.0965,1565.0965,1589.054,1613.0115,1635.8423,1635.8423,1635.8423,1660.7581
					,1685.6739,1710.4213,1710.4213,1710.4213,1735.3371,1760.2529,1786.127,1787.8752,1787.8752,1813.7493
					,1839.6234,1866.4558,1893.2882,1896.953,1896.953,1923.7854,1951.5761,1979.3668,2007.1575,2012.7389
					,2012.7389,2041.4879,2070.2369,2098.9859,2127.7349,2156.4839,2185.2329,2214.9402,2244.6475,2274.3548
					,2304.0621,2333.7694,2363.4767,2394.1423,2424.8079,2455.4735,2486.1391,2516.8047,2547.4703,2578.1359
					,2608.8015};
				for (int t = 0; t < 500; t++)
				{
					double minerals;
					double gas;
					//set mineral amount
					if (t < 244)
					{
						minerals = first244MineralCounts[t];
					}
					else { minerals = first244MineralCounts[243] + (t - 243) * 15.33 * 2; }

					//set gas amount
					//assume geysers done at 72 and 126
					if (t < 72) { gas = 0; }
					else if (t < 126) { gas = (t - 67) * 3 * 0.94228; }
					else { gas = (t - 79) * 6 * 0.94228 + (80 - 67) * 3 * 0.94228; }
					BankStatement newStatement = new BankStatement(minerals, gas);
					approxFunction.Add(newStatement);
				}
				return approxFunction;
			}
			else if (ecoOption == 3)
			{
				List<BankStatement> approxFunction = new List<BankStatement>();
				double minerals;
				double gas;
				for (int t = 0; t < 500; t++)
				{
					minerals = t * 6 + 0.03 * Math.Pow(t, 2) - 0.000005 * Math.Pow(t, 3);
					gas = 0;
					if (t > 200) { gas = (t - 160) * 12; }
					else if (t > 72) { gas = (t - 72) * 6; }
					BankStatement newStatement = new BankStatement(minerals, gas);
					approxFunction.Add(newStatement);
				}
				return approxFunction;
			}
			else if (ecoOption == 4)
			{
				List<BankStatement> approxFunction = new List<BankStatement>();
				double minerals;
				double gas;
				for (int t = 0; t < 500; t++)
				{
					minerals = t * 10 + 0.02 * Math.Pow(t, 2) - 0.000005 * Math.Pow(t, 3);
					gas = 0;
					if (t > 400) { gas = (t - 300) * 12; }
					else if (t > 100) { gas = (t - 100) * 6; }
					BankStatement newStatement = new BankStatement(minerals, gas);
					approxFunction.Add(newStatement);
				}
				return approxFunction;
			}
			else if (ecoOption == 5)
			{
				//try forcing 1 gate expand (pylon 17, gateway 36, cyber 1:32) at this point at 20 supply
				//then 2nd gas and2nd pylon ->have free income at 1:50
				//then have ~15 minerals per second and ~ 2.8 gas per second

				List<BankStatement> approxFunction = new List<BankStatement>();
				//have initial function go on for 500 seconds 
				//mimic 1gate fe with 2 gas total progression
				List<double> first101MineralCounts = new List<double> {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,100,100,100,100,100,100,
					100,100,100,100,100,100,100,100,100,100,100,100,100,250,250,250,250,250,250,250,250,250,250,250,250,250,250,
					250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,
					250,250,250,250,250,250,250,250,250,250,250,250,250,250,250,400,400,400,400,400,400,400,400,400,400,400,400,400,400,400,400,400,400 };
				for (int t = 0; t < 500; t++)
				{
					double minerals;
					double gas;
					//set mineral amount
					if (t < 110)
					{
						minerals = first101MineralCounts[t];
					}
					else { minerals = first101MineralCounts[109] + (t - 109) * 15.33; }

					//set gas amount
					//assume geysers done at 80 and 120
					if (t < 80) { gas = 0; }
					else if (t < 120) { gas = (t - 80) * 3 * 0.94228; }
					else { gas = (t - 120) * 6 * 0.94228 + (120 - 80) * 3 * 0.94228; }
					BankStatement newStatement = new BankStatement(minerals, gas);
					approxFunction.Add(newStatement);
				}
				return approxFunction;
			}
			else
			{
				List<BankStatement> approxFunction = new List<BankStatement>();
				double minerals;
				double gas;
				for (int t = 0; t < 500; t++)
				{
					minerals = t * 4 + 0.03 * Math.Pow(t, 2) - 0.000005 * Math.Pow(t, 3);
					gas = 0;
					if (t > 65) { gas = (t - 65) * 6; }
					else if (t > 30) { gas = (t - 30) * 3; }
					BankStatement newStatement = new BankStatement(minerals, gas);
					approxFunction.Add(newStatement);
				}
				return approxFunction;
			}
		}
		private static List<BankStatement> findFreeCashFunction(dynamicEconBuildRunnerClass finalBuild)
		{
			List<bUUDataObject> bUUPairsLibrary = finalBuild.pairObjectsLibrary;


			//only make freeCashFunction 1000% the endTime of finalBuild
			//int numBankStatementsNeeded = (int)Math.Ceiling(finalBuild.endTime*10);
			//for now make 500
			int numBankStatementsNeeded = 500;
			List<BankStatement> freeCashFunction = new List<BankStatement>(numBankStatementsNeeded);

			for (int i = 0; i < numBankStatementsNeeded; i++)
			{
				BankStatement newBankStatement = new BankStatement(finalBuild.bankStatementList[i].mineralBank, finalBuild.bankStatementList[i].gasBank);
				freeCashFunction.Add(newBankStatement);
			}
			bool firstPylonFound = false;
			int entryNum = 0;
			foreach (buildOrderEntry thisEntry in finalBuild.realizedBuildLog)
			{
				//if this is economic element, find amount of cash available after purchase, and make this value constant going backwards
				if (thisEntry.itemToBuild.Equals("Probe") || thisEntry.itemToBuild.Equals("assimilator") || thisEntry.itemToBuild.Equals("nexus") || (thisEntry.itemToBuild.Equals("pylon") && firstPylonFound))
				{
					//if a noneconomic element occurs doing this same second but after this element, then mineralsLeft should have that amount added to it
					//look ahead to see if any nonEco elmeents occur in this same interval

					int thisTime = (int)Math.Ceiling(thisEntry.timeToBuild);
					double extraMineralsSpentThisSecond = 0;
					for (int i = entryNum + 1; i < finalBuild.realizedBuildLog.Count(); i++)
					{
						if (finalBuild.realizedBuildLog[i].timeToBuild <= thisTime)
						{
							//check if nonEco element
							if (!(finalBuild.realizedBuildLog[i].itemToBuild.Equals("Probe") || finalBuild.realizedBuildLog[i].itemToBuild.Equals("assimilator") || finalBuild.realizedBuildLog[i].itemToBuild.Equals("nexus") || (firstPylonFound && finalBuild.realizedBuildLog[i].itemToBuild.Equals("pylon"))))
							{
								//find additional mineral Cost
								extraMineralsSpentThisSecond = extraMineralsSpentThisSecond + bUUPairsLibrary.First(x => x.name.Equals(finalBuild.realizedBuildLog[i].itemToBuild)).mineralCost;
							}
						}
						else { break; }
					}
					double mineralsLeft = freeCashFunction[thisTime].mineralBank + extraMineralsSpentThisSecond;
					//set all preceding values greater than mineralsLeft to minerals left
					for (int i = thisTime - 1; i >= 0; i--)
					{
						if (freeCashFunction[i].mineralBank > mineralsLeft)
						{ freeCashFunction[i].mineralBank = mineralsLeft; }
						else { break; }
					}
				}

				//if this if not economic element, add to all BankStatements after set one
				if (!(thisEntry.itemToBuild.Equals("Probe") || thisEntry.itemToBuild.Equals("assimilator") || thisEntry.itemToBuild.Equals("nexus")))
				{
					if (!(firstPylonFound && thisEntry.itemToBuild.Equals("pylon")))
					{
						if (thisEntry.itemToBuild.Equals("pylon"))
						{
							firstPylonFound = true;
						}
						bUUDataObject thisObject = bUUPairsLibrary.First(x => x.name.Equals(thisEntry.itemToBuild));
						int firstTimeToAdd = (int)Math.Ceiling(thisEntry.timeToBuild);
						for (int i = firstTimeToAdd; i < numBankStatementsNeeded; i++)
						{
							freeCashFunction[i].mineralBank = freeCashFunction[i].mineralBank + thisObject.mineralCost;
							freeCashFunction[i].gasBank = freeCashFunction[i].gasBank + thisObject.gasCost;
						}
					}
				}
				entryNum++;
			}
			return freeCashFunction;
		}

		public static buildOrderWithEndTime dynamicEconSingleBuildRunnerAdmin(buildCreatorPreEcon thisBuild, List<buildOrderEntry> compressedBuild, coefficientPackage thisCoefficientPackage)
		{
			dynamicEconBuildRunnerClass newBuild = new dynamicEconBuildRunnerClass(thisBuild, compressedBuild, thisCoefficientPackage, false);
			double runTime = newBuild.endTime;
			buildOrderWithEndTime thisBO = new buildOrderWithEndTime(runTime, newBuild.realizedBuildLog);
			return thisBO;
		}
	}
}
