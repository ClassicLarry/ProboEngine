using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
    public class backwardsApproach
    {
        private List<IDWithList> productionTable { get; set; }
        private List<bUUEvent> eventList { get; set; }
        private List<PairsTable> pairList { get; set; }
        private List<bUUDataObject> pairObjectLibrary { get; set; }
        private List<IDWithList> precedenceRelations { get; set; }
        public constructBestBUUOrder bestYet { get; set; }
        public double bareBonesMineralCost { get; set; }
        public double bareBonesGasCost { get; set; }
        public double maxExcessMineralCost { get; set; }
        public double maxExcessGasCost { get; set; }
        public double fastestTimeYet { get; set; }
        public backwardsApproach(List<bUUDataObject> myPairObjectLibrary, List<bUUPairObject> buildingPairs, List<BankStatement> freeCashFunction, List<double> approximateChronoTimes, double oldFastestRun, int globalIterationNum,bool firstChronoSelected)
        {
			//Initialize params and then iterate through different combinations of # of production buildings and units per production building
            fastestTimeYet = oldFastestRun*1.05; //give 5% cushion incase nothing in this run is faster than old run
            pairObjectLibrary = myPairObjectLibrary;

            //create primary buildings pairs and event list (use dummy times for now)
            createPrimaryEventAndPairList(buildingPairs);

            //add unit events to eventList
            createUnitEvents();

            //create precedence relations
            createPrecedenceRelations();

            //create production table
            createProductionTable();

            //find minimum resource cost
            calculateBareBonesCosts();

            if (fastestTimeYet < 100000) //if not first run
            {
                int fastestTime = (int)Math.Ceiling(fastestTimeYet);
                maxExcessMineralCost = freeCashFunction[fastestTime].mineralBank - bareBonesMineralCost;
                maxExcessGasCost = freeCashFunction[fastestTime].gasBank - bareBonesGasCost;
            }
            else
            {
                maxExcessMineralCost = 1000000;
                maxExcessGasCost = 10000000;
            }
			//check if WGR is included, and find location
			int WGRBUUDataID = findWGRDataID(); //-1 if not included
												//convert BankStatements to List<doubles> for faster access
			List<double> gasStatements = convertGasStatements(freeCashFunction);
			List<double> mineralStatements = convertMineralStatements(freeCashFunction);
			List<double> alteredMineralStatementsBlank = new List<double>(mineralStatements);
			double numChronoOptionsRan = 0;
			double numBasketsSplitRan = 0;
			List<List<int>> allBasketCounts = createDifferentBasketCounts();
			List<int> bestBasketCount = new List<int>();
			foreach (List<int> thisBasketCount in allBasketCounts)
			{
				//reduce runtime for testing: if already run 10k options and have a best yet, exit program
				if (numChronoOptionsRan > 10000 && fastestTimeYet < 10000) { break; }


				//check if this BasketCount is in viable zone
				double excessMinerals = calculateExcessMinerals(thisBasketCount);
				double excessGas = calculateExcessGas(thisBasketCount);

				bool isViable = checkIfNumBasketsViable(excessMinerals, excessGas);
				if (isViable)
				{
					List<IDWithList> evenSplit = findEvenSplit(thisBasketCount);
					//run build for this split to get a baseline time with even split
					constructBestBUUOrder firstTime = new constructBestBUUOrder(fastestTimeYet, WGRBUUDataID, pairObjectLibrary, productionTable, evenSplit, eventList, pairList, precedenceRelations, alteredMineralStatementsBlank, mineralStatements, gasStatements, approximateChronoTimes, globalIterationNum, firstChronoSelected);
					numChronoOptionsRan = numChronoOptionsRan + firstTime.numChronoFunctionsRan;
					numBasketsSplitRan++;
					if (!firstTime.discardBuild)
					{
						if (firstTime.bestChronoOptionEvaluation.buildEndTime < fastestTimeYet)
						{
							fastestTimeYet = firstTime.bestChronoOptionEvaluation.buildEndTime;
							bestYet = firstTime;
							updateExcessProperties(mineralStatements, gasStatements);
							bestBasketCount = thisBasketCount;
						}
					}

					List<IDWithList> lastSplit = evenSplit;
					//repeat new splits until done (first item will be -1 if done). Max at 1000 different splits
					for (int i = 0; i < 1000; i++)
					{
						lastSplit = findNextSplit(thisBasketCount, lastSplit, i == 0);
						if (lastSplit[0].ID == -1)
						{ break; }
						if (checkIfSplitViable(lastSplit))
						{
							//run build for this split
							constructBestBUUOrder newBest = new constructBestBUUOrder(fastestTimeYet, WGRBUUDataID, pairObjectLibrary, productionTable, lastSplit, eventList, pairList, precedenceRelations, alteredMineralStatementsBlank, mineralStatements, gasStatements, approximateChronoTimes, globalIterationNum, firstChronoSelected);
							numChronoOptionsRan = numChronoOptionsRan + newBest.numChronoFunctionsRan;
							numBasketsSplitRan++;
							if (!newBest.discardBuild)
							{
								if (newBest.bestChronoOptionEvaluation.buildEndTime < fastestTimeYet)
								{
									fastestTimeYet = newBest.bestChronoOptionEvaluation.buildEndTime;
									bestYet = newBest;
									updateExcessProperties(mineralStatements, gasStatements);
									bestBasketCount = thisBasketCount;

								}
							}

						}
						if (i == 300)
						{
							int h = 0;
							break;
						}
					}
				}
			}

			//edit bestBuild start times to only consider precedence and not economy (format neccessary for economic insertion)
			//different methods depending on if build contains WGR and gateway units
			int gatewayBUUID = pairObjectLibrary.First(x => x.name.Equals("gateway")).bUUDataID;

			if (pairObjectLibrary.Where(x => x.name.Equals("warpGateResearch")).Any() && productionTable.Where(x => x.ID == gatewayBUUID).Any())
			{
				onlyConsiderPrecedenceWithWGR();
			}
			else
			{
				onlyConsiderPrecedenceWithoutWGR();
			}
		}

        private List<double> convertMineralStatements(List<BankStatement> cashFunction)
        {
            List<double> newList = new List<double>(cashFunction.Count());
            foreach (BankStatement thisStatement in cashFunction)
            {
                newList.Add(thisStatement.mineralBank);
            }
            return newList;
        }
        private List<double> convertGasStatements(List<BankStatement> cashFunction)
        {
            List<double> newList = new List<double>(cashFunction.Count());
            foreach (BankStatement thisStatement in cashFunction)
            {
                newList.Add(thisStatement.gasBank);
            }
            return newList;
        }
        private void onlyConsiderPrecedenceWithWGR()
        {
			//use durations from bestBuild, use pairs from bestYet. Find minTimes based on precedence only 
			//To Remove economy dependence: See how far WGR components can be right shifted without breaking precedence.
			double wiggleRoom = bestYet.bestChronoOptionEvaluation.identifyWGRComponentsWiggleRoom(bestYet.bestBuild);
			//Left shift all other elements by this amount
			//left shift all elements by pylon StartTime
			//max shift is (endTime - WGRendTime + pylonStartTime) ->WGR should not be finishing 20 seconds after rest of build
			double pylonStartTime = bestYet.bestBuild[pairObjectLibrary.First(x => x.name.Equals("pylon")).bUUEventIDForPrimaryBuilding].startTime;
			double WGREndTime = bestYet.bestBuild[pairObjectLibrary.First(x => x.name.Equals("warpGateResearch")).bUUEventIDForPrimaryBuilding].endTime;
			double buildEndTime = bestYet.bestBuild.Max(x => x.endTime);
			double maxShift = buildEndTime - WGREndTime + pylonStartTime;
			double amountToShift = pylonStartTime + wiggleRoom;
			if (amountToShift > maxShift) { amountToShift = maxShift; }
			foreach (bUUEvent thisEvent in bestYet.bestBuild)
			{
				//check if WGR Component
				if (bestYet.WGREventChain.Contains(thisEvent.bUUEventID))
				{
					thisEvent.startTime = thisEvent.startTime - pylonStartTime;
					thisEvent.endTime = thisEvent.endTime - pylonStartTime;
				}
				else
				{
					thisEvent.startTime = thisEvent.startTime - amountToShift;
					thisEvent.endTime = thisEvent.endTime - amountToShift;
				}
			}
			
        }
        private void onlyConsiderPrecedenceWithoutWGR()
        {
			//use durations from bestBuild, use pairs from bestYet. Find minTimes based on precedence only 
			//To Remove economy dependence: Shift all events based on pylon start time (events have only been shifted as a unified group previously)
			if (bestYet != null)
			{
				double pylonStartTime = bestYet.bestBuild[pairObjectLibrary.First(x => x.name.Equals("pylon")).bUUEventIDForPrimaryBuilding].startTime;
				foreach (bUUEvent thisEvent in bestYet.bestBuild)
				{
					thisEvent.startTime = thisEvent.startTime - pylonStartTime;
					thisEvent.endTime = thisEvent.endTime - pylonStartTime;
				}
			}
            
        }
        private List<IDWithList> findNextSplit(List<int> thisBasketCount, List<IDWithList> lastSplit, bool firstTime)
        {
            if (firstTime)
            {
                //return max amount in first column
                List<IDWithList> firstSplit = new List<IDWithList>();
                List<int> itemsToAddToFirstBuilding = new List<int>();
                for (int i = 0; i < productionTable.Count(); i++)
                {
                    int numItems = productionTable[i].IDList.Count() - thisBasketCount[i]+1;
                    List<int> splitForThisRow = new List<int>();
                    splitForThisRow.Add(numItems);
                    //add 1 to every other basket
                    for (int g = 1; g < thisBasketCount[i]; g++)
                    {
                        splitForThisRow.Add(1);
                    }
                    IDWithList newEntry = new IDWithList(productionTable[i].ID, splitForThisRow);
                    firstSplit.Add(newEntry);
                }
                return firstSplit;
            }
            //at least one unit must go into each basket
            //next unit after that is defaulted to first basket
            //each production building has (#units-#baskets-1) booleans
            //boolean is comprised of X digits, where X represents sum of all production building booleans
            //when checking a digit, find which Row it belongs to, compare rules against properties of this row

            //convert lastSplit into boolean representation
            List<IDWithList> boolRepresentation = convertSplitToBool(lastSplit);


            List<int> boolsPerRow = new List<int>();
            for (int i = 0; i < productionTable.Count(); i++)
            {
                int numBools = productionTable[i].IDList.Count() - thisBasketCount[i] - 1;
                boolsPerRow.Add(numBools);
            }
            //work backwards through each element, find first 0 that can be made 1
            //default all elements after to 0
            bool nextSplitFound = false;
            for (int i = lastSplit.Count() - 1; i >= 0; i--)
            {
                for (int c = boolRepresentation[i].IDList.Count() - 1; c >= 0; c--)
                {
                    if (checkCanChangeBasket(i, c, boolRepresentation, thisBasketCount))
                    {
                        boolRepresentation = incrementSplitHere(i, c, boolRepresentation, thisBasketCount);
                        nextSplitFound = true;
                        break;
                    }
                }
                if (nextSplitFound) { break; }
            }
            if (!nextSplitFound) //if this is final split, let parent function know
            {
                lastSplit[0].ID = -1;
                return lastSplit;
            }
            //convert boolRepresentation back
            List<IDWithList> nextSplit = convertBoolRepBack(boolRepresentation, thisBasketCount);
            return nextSplit;
        }
        private List<IDWithList> convertBoolRepBack(List<IDWithList> boolRepresentation, List<int> thisBasketCount)
        {
            List<IDWithList> listToReturn = new List<IDWithList>();
            for (int a = 0; a < boolRepresentation.Count(); a++)
            {
                int thisID = boolRepresentation[a].ID;
                List<int> thisSplit = new List<int>();
                //first add default members 
                thisSplit.Add(1);
                //(add two to first bucket if have the units)
                if (productionTable[a].IDList.Count() > thisBasketCount[a])
                {
                    thisSplit[0]++;
                }
                for (int i = 1; i < thisBasketCount[a]; i++)
                {
                    thisSplit.Add(1);
                }
                //next add members as determined by bool
                int currentBasket = 0;
                for (int c = 0; c < boolRepresentation[a].IDList.Count(); c++)
                {
                    if (boolRepresentation[a].IDList[c] == 1) { currentBasket++; }
                    thisSplit[currentBasket]++;
                }
                IDWithList newEntry = new IDWithList(thisID, thisSplit);
                listToReturn.Add(newEntry);
            }
            return listToReturn;
        }
        private List<IDWithList> incrementSplitHere(int row, int thisDecision, List<IDWithList> boolRepresentation, List<int> thisBasketCount)
        {
            //change spot to 1
            boolRepresentation[row].IDList[thisDecision] = 1;
            //attempt to change all future spots to 0
            for (int i = row; i < boolRepresentation.Count(); i++)
            {
                for (int c = 0; c < boolRepresentation[i].IDList.Count(); c++)
                {
                    //if its the first row, only start after thisDecision
                    if (i!=row || c > thisDecision)
                    {
                        boolRepresentation = pickSplitDecision(boolRepresentation, i, c);
                    }
                }
            }
            return boolRepresentation;
        }
        private List<IDWithList> pickSplitDecision(List<IDWithList> boolRepresentation, int row, int thisSpot)
        {
            //make this Spot 0 if possible, else make 1
            //base decision on following rules based on previous bools in that row:
            //only make true if basket is same size as previous basket
            bool isThisBasket = true;
            int previousBasketSize = 0;
            int thisBasketSize=0;
            bool previousBasketIsFirst = true;
            for (int i = thisSpot - 1; i >= 0; i--)
            {
                if (isThisBasket)
                {
                    thisBasketSize++;
                    if (boolRepresentation[row].IDList[i] == 1)
                    {
                        isThisBasket = false;
                    }
                }
                else
                {
                    previousBasketSize++;
                    if (boolRepresentation[row].IDList[i] == 1)
                    {
                        previousBasketIsFirst = false;
                        break;
                    }
                }
            }
            if (previousBasketIsFirst) { previousBasketSize++; }
            if (thisBasketSize == previousBasketSize && !isThisBasket) //if its the first basket, make 0
            {
                //need to make one
                boolRepresentation[row].IDList[thisSpot] = 1;
            }
            else
            {
                //make 0
                boolRepresentation[row].IDList[thisSpot] = 0;
            }
            return boolRepresentation;
        }
        private bool checkCanChangeBasket(int row, int thisDecision, List<IDWithList> boolRepresentation, List<int> thisBasketCount)
        {
            //looking to see if item can be changed from 0 to 1
            //if item is already 1, return false;
            if (boolRepresentation[row].IDList[thisDecision] == 1) { return false; }

            //check if rules are maintained for if item can move from 0 to 1:

            //1. if its the last basket, cant make 1
            //check if the rest of elements in this Row are all 0
            //(checked at end of part 2)

            //2. If thisBasketCount<(numUnitsLeft/numBasketsLeft), cant make 1
            //find which basket this is (see how many 1s exist in this row before this element
            int thisBasket=1; //(assume its first basket)
            for (int i = 0; i < thisDecision; i++)
            {
                if (boolRepresentation[row].IDList[i] == 1) { thisBasket++; }
            }
            //find how many units are currently in this basket (#0s before this element, also add the first one) add +1 if first basket
            //ignore the first unit that is put into every basket
            int numUnits = 0;
            if (thisBasket == 1) { numUnits = 1; }
            for (int i = thisDecision - 1; i >= 0; i--)
            {
                numUnits++;
                if (boolRepresentation[row].IDList[i] == 1)
                {
                    break;
                }
            }

            int numUnitsLeft=boolRepresentation[row].IDList.Count() - (thisDecision+1) + 1;
            int basketsLeft = thisBasketCount[row] - thisBasket;
            if (basketsLeft == 0) { return false; }
            double maxSize = (double)numUnitsLeft / (double)basketsLeft;
            if ((double)numUnits < maxSize) { return false; }

            //if those conditions arent met, return true (can change to 1).
            return true;
        }
        private List<IDWithList> convertSplitToBool(List<IDWithList> lastSplit)
        {
            //create rows of 1s and 0s based on when split was made
            //0= stay in this basket, 1= move to next basket
            //Example a split of (5,4,2) -> free choices (3,3,2) -> bools are (0,0,0,1,0,0,1,0), where some of the bools are forced into 1 option (like last 1)

            List<IDWithList> boolRepresentation = new List<IDWithList>();
            //go through each production building split directions
            for (int j = 0; j < lastSplit.Count(); j++)
            {
                //create a new row
                int thisID = lastSplit[j].ID;
                List<int> thisBinaryRow = new List<int>();
                //only add elements if there are multiple baskets in this row and numElements>numbaskets+1
                if (lastSplit[j].IDList.Count() > 1 && productionTable[j].IDList.Count>lastSplit[j].IDList.Count()+1)
                    for (int i = 0; i < lastSplit[j].IDList.Count(); i++)
                    {
                        //first basket has 2 defaults, all other baskets have 1 default
                        if (i == 0)
                        {
                            int numberDecisionsToStay = lastSplit[j].IDList[i] - 2;
                            for (int c = 0; c < numberDecisionsToStay; c++)
                            {
                                thisBinaryRow.Add(0);
                            }

                        }
                        else
                        {
                            //if going to add zeroes for a basket, add a 1 for first element that swapped over
                            int numberDecisionsToStay = lastSplit[j].IDList[i] - 1; 
                            if (numberDecisionsToStay > 0) { thisBinaryRow.Add(1); }
                            for (int c = 1; c < numberDecisionsToStay; c++) //start at 1 because first element is added as a 1
                            {
                                thisBinaryRow.Add(0);
                            }
                        }
                    }
                IDWithList newEntry = new IDWithList(thisID, thisBinaryRow);
                boolRepresentation.Add(newEntry);
            }
           
            return boolRepresentation;
        }
        private List<IDWithList> findEvenSplit(List<int> thisBasketCount)
        {
            //consider even split
            List<IDWithList> evenSplit = new List<IDWithList>();
            for (int i = 0; i < thisBasketCount.Count(); i++)
            {
                //create new row in evenSplit
                int rowID = productionTable[i].ID;
                List<int> evenList = buildEventList(thisBasketCount[i], productionTable[i].IDList.Count());
                IDWithList newEntry = new IDWithList(rowID, evenList);
                evenSplit.Add(newEntry);
            }
            return evenSplit;
        }
        private List<int> buildEventList(int numberBaskets, int numberUnits)
        {
            List<int> unitsPerBasket = new List<int>();
            for (int i = 0; i < numberBaskets; i++)
            {
                unitsPerBasket.Add(0);
            }

            //now go through and add units to baskets
            int numUnitsAdded = 0;
            while (numUnitsAdded < numberUnits)
            {
                for (int i = 0; i < numberBaskets; i++)
                {
                    unitsPerBasket[i]++;
                    numUnitsAdded++;
                    if (numUnitsAdded >= numberUnits) { break; }
                }
            }
            return unitsPerBasket;
        }
        private void updateExcessProperties(List<double> freeMinerals, List<double> freeGas)
        {
            //check excess minerals and gas available and fastestTimeYet
            int fastestTime = (int)Math.Ceiling(fastestTimeYet);
            maxExcessMineralCost = freeMinerals[fastestTime] - bareBonesMineralCost;
            maxExcessGasCost = freeGas[fastestTime] - bareBonesGasCost;
        }
        private bool checkIfSplitViable(List<IDWithList> thisSplit)
        {
            //check to see if this split has a single basket with a longer total run time than shortest build
            //estimate a basket runtime as firstElementRunTime*#elements
            for (int i = 0; i < thisSplit.Count(); i++)
            {
                foreach (int numUnits in thisSplit[i].IDList)
                {
                    int unitDurationEstimate = pairObjectLibrary[productionTable[i].IDList[0]].duration;
                    double totalETA = numUnits * unitDurationEstimate;
                    if (totalETA > fastestTimeYet * 1) //leave as *1 for now
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        private bool checkIfNumBasketsViable(double excessMinerals, double excessGas)
        {
            //assume that chronoBoost can give a build at most 10% more economy
            if (excessMinerals*.9>maxExcessMineralCost || excessGas*.9 > maxExcessGasCost) { return false; }
            else { return true; }
        }
        private double calculateExcessMinerals(List<int> thisBasketCount)
        {
            double excessMinerals = 0;
            for (int i = 0; i < thisBasketCount.Count(); i++)
            {
                if (thisBasketCount[i] > 1)
                {
                    int numExtra = thisBasketCount[i] - 1;
                    excessMinerals = excessMinerals + numExtra*pairObjectLibrary[productionTable[i].ID].mineralCost;
                }
            }
            return excessMinerals;
        }
        private double calculateExcessGas(List<int> thisBasketCount)
        {
            double excessGas = 0;
            for (int i = 0; i < thisBasketCount.Count(); i++)
            {
                if (thisBasketCount[i] > 1)
                {
                    int numExtra = thisBasketCount[i] - 1;
                    excessGas = excessGas + numExtra * pairObjectLibrary[productionTable[i].ID].gasCost;
                }
            }
            return excessGas;
        }

        private List<List<int>> createDifferentBasketCounts()
        {
            List<List<int>> allDistributions = new List<List<int>>();
            List<int> numUnits = new List<int>();
            List<int> nextDistribution = new List<int>();
            foreach (IDWithList thisRow in productionTable)
            {
                int unitCount;
                //max out forge, cyber at num different types upgrades
                if (thisRow.ID == pairObjectLibrary.First(x => x.name == "forge").bUUDataID)
                {
                    unitCount = findMaxNumForges();
                }
                else if (thisRow.ID == pairObjectLibrary.First(x => x.name == "cyberCore").bUUDataID)
                {
                    //for now only have one cyber core, errors with warpgatereaserach
                    //unitCount = findMaxNumCybers();
                    unitCount = 1;
                }
                else
                {
                    unitCount = thisRow.IDList.Count();
                }
                numUnits.Add(unitCount);
                nextDistribution.Add(1);
            }
            allDistributions.Add(nextDistribution);

            for (int i = 0; i < 100000; i++)
            {
                nextDistribution = findNextDistribution(nextDistribution, numUnits);
                if (nextDistribution[0] == 0)
                {
                    break;
                }
                allDistributions.Add(nextDistribution);
            }
            return allDistributions;
        }
        private int findMaxNumForges()
        {
            int returnVal = 0;
            //check for +1 wp, +1 armor, +1 shields
            if (pairObjectLibrary.Where(x => x.name == "groundWeapons1").Any()) { returnVal++; }
            if (pairObjectLibrary.Where(x => x.name == "groundArmor1").Any()) { returnVal++; }
            if (pairObjectLibrary.Where(x => x.name == "shields1").Any()) { returnVal++; }
            return returnVal;
        }
        private int findMaxNumCybers()
        {
            int returnVal = 0;
            //check for +1 wp, +1 armor, +1 shields
            if (pairObjectLibrary.Where(x => x.name == "airWeapons1").Any()) { returnVal++; }
            if (pairObjectLibrary.Where(x => x.name == "airArmor1").Any()) { returnVal++; }
            if (pairObjectLibrary.Where(x => x.name == "warpGateResearch").Any()) { returnVal++; }
            return returnVal;
        }
        private List<int> findNextDistribution(List<int> lastDistribution, List<int> numUnits)
        {
            List<int> thisDistribution = new List<int>(lastDistribution);
            //go through lastDistribution. make first item possible 1 bigger. 
            //make all previous items 
            for (int i = 0; i < lastDistribution.Count(); i++)
            {
                if (lastDistribution[i] < numUnits[i])
                {
                    //add one to this spot
                    thisDistribution[i]++;
                    //make all previous spots 1
                    for (int g = i - 1; g >= 0; g--)
                    {
                        thisDistribution[g] = 1;
                    }
                    return thisDistribution;
                }
            }
            //will only reach here once done
            thisDistribution[0] = 0;
            return thisDistribution;
        }
        private void calculateBareBonesCosts()
        {
            double mineralCost=0;
            double gasCost = 0;
            foreach (bUUEvent thisEvent in eventList)
            {
                mineralCost = mineralCost + pairObjectLibrary[thisEvent.bUUDataID].mineralCost;
                gasCost = gasCost + pairObjectLibrary[thisEvent.bUUDataID].gasCost;
            }
            bareBonesMineralCost = mineralCost;
            bareBonesGasCost = gasCost;
        }
        private int findWGRDataID()
        {
            int spot = -1;
            List<bUUDataObject> WGRList = pairObjectLibrary.Where(x => x.name.Equals("warpGateResearch")).ToList();
            if (WGRList.Any())
            {
                spot = WGRList[0].bUUDataID;
            }
            return spot;
        }
        private void createUnitEvents()
        {
            //go through pairObjectLibrary and create an event for each unit. include dummy pairs for now. add reference in pairObjectLibrary
            foreach (bUUDataObject thisObject in pairObjectLibrary)
            {
                if (!thisObject.isBuilding && thisObject.mineralCost > 0 && !thisObject.name.Equals("Probe")) //don't include warpgate or probe
                {
                    bUUEvent newEvent = new bUUEvent(eventList.Count(), 0, 0, thisObject.bUUDataID, -1, -1);
                    thisObject.bUUEventIDForPrimaryBuilding = eventList.Count();
                    eventList.Add(newEvent);
                }
            }
        }
        private void createPrecedenceRelations()
        {
            //need a dataObject: [thisEventID] List[all units that have precedence on thisEventID, or building that is child]
            //pairList is sorted in technological order, precedenceRelations should be sorted in same manner
            precedenceRelations = new List<IDWithList>();

            //go through eventList and make entry for each event
            foreach (bUUEvent thisEvent in eventList)
            {
                //make sure this event is a building
                if (pairObjectLibrary[thisEvent.bUUDataID].isBuilding)
                {
                    List<int> newList = new List<int>();
                    IDWithList newEntry = new IDWithList(thisEvent.bUUEventID, newList);
                    //add all children of this event
                    List<PairsTable> children = pairList.Where(x => x.parentBUUEventID == thisEvent.bUUEventID).ToList();
                    foreach (PairsTable thisChild in children)
                    {
                        //add the child eventID if nonnegative number (negatives correspond to no child)
                        if (thisChild.childBUUEventID >= 0)
                        {
                            newEntry.IDList.Add(thisChild.childBUUEventID);
                        }
                    }
                    //only add to list if has precedence relations
                    precedenceRelations.Add(newEntry);
                }
            }
            //now add precedence for each unit
            //go through pairObjectLibrary, and consider the tech reqs of each unit
            foreach (bUUDataObject thisObject in pairObjectLibrary)
            {
                if(!thisObject.isBuilding && thisObject.mineralCost>0) //dont consider warpgate
                {
                    int thisEventID = thisObject.bUUEventIDForPrimaryBuilding;
                    foreach (string thisTechReq in thisObject.buildingReqList)
                    {
                        //find eventID of the tech req. add thisEventID to that row in precedenceRelations
                        int techReqEventID = pairObjectLibrary.First(x => x.name.Equals(thisTechReq)).bUUEventIDForPrimaryBuilding;

                        //techreqEventID may be for an earlier upgrade, in which case it wont exist in precendence relations
                        if (precedenceRelations.Where(x => x.ID == techReqEventID).Any())
                        {
                            precedenceRelations.First(x => x.ID == techReqEventID).IDList.Add(thisEventID);
                        }
                        
                    }
                }
            }
        }
        private void createPrimaryEventAndPairList(List<bUUPairObject> buildingPairs)
        {
            //convert input to new format
            eventList = new List<bUUEvent>();
            pairList = new List<PairsTable>();

            foreach (bUUPairObject thisPair in buildingPairs)
            {
                if (thisPair.child.name.Equals("pylon")) //create event but not pair
                {
                    int eventID = eventList.Count();
                    bUUEvent newEvent = new bUUEvent(eventID, 0, 0, thisPair.child.bUUDataID, -1, -1);
                    eventList.Add(newEvent);
                }
                else if (thisPair.child.isBuilding)
                {
                    //find parent Event
                    int parentEventID = eventList.First(x => x.bUUDataID == thisPair.parent.bUUDataID).bUUEventID;

                    int eventID = eventList.Count();
                    int pairID = pairList.Count();
                    bUUEvent newEvent = new bUUEvent(eventID, 0, 0, thisPair.child.bUUDataID, pairID, -1);
                    eventList.Add(newEvent);
                    PairsTable newPair = new PairsTable(pairID, parentEventID, eventID);
                    pairList.Add(newPair);

                    //edit parentEvent to correctly use pairAsParentID
                    eventList[parentEventID].pairAsParentID = pairID;

                    //add eventID to pairsLibrary for quick access
                    pairObjectLibrary[thisPair.child.bUUDataID].bUUEventIDForPrimaryBuilding = eventID;
                }
            }

        }
        private List<bool> createRandomBasketSplit(int numUnits, int numProdBuildings)
        {
            //for each row in production table create (#elements-1) booleans
            //true=(startNextBasket), false=(stay)
            //past order may require T or F
            //need some manner to iterate through options from all F to all T
            //iterate through in a manner that goes from fewest buildings to most buildings
            
            //treat the combination of bools as a value
            //compare two values by finding first instance (from top to bottom) where 1>0
            //iterate through by making first entry found a 1
            List<List<bool>> splitsInOrder = new List<List<bool>>();
            int numEntries = numUnits - numProdBuildings;
            List<bool> firstList = new List<bool>();
            for (int i = 0; i < numEntries; i++)
            {
                firstList.Add(false);
            }
            splitsInOrder.Add(firstList);

            for (int i = 0; i < 100000; i++)
            {
                //follow T/F rules as per each set
                for (int spot = 0; spot < numEntries; spot++)
                {

                }
            }

            //split information is comprised of numUnits-numProdBuildings booleans, where x is number of units, and r is number of production buildings
            //create bool list
            Random rando = new Random();
            List<bool> listToReturn = new List<bool>();
            for (int g = 0; g < numEntries; g++)
            {
                listToReturn.Add(rando.Next(3) == 1);
            }
            return listToReturn;
        }
        private List<IDWithList> createBasketSplitTable(List<bool> basketSplitDirections)
        {
            List<IDWithList> basketSplitTable = new List<IDWithList>();
            //foreach element in productionTable, consider its list
            //foreach element in its list, consider option to (add another production building)
            int totalElementsChecked = 0;
            int previousAddedElements = 0;
            foreach (IDWithList thisRow in productionTable)
            {
                List<int> splitForThisRow = new List<int>();
                //split elements in this Row based on basketSplitDirections
                for (int i = 0; i < thisRow.IDList.Count() - 1; i++)
                {
                    //check boolean at this spot
                    if (basketSplitDirections[totalElementsChecked])
                    {
                        //create a new basket
                        splitForThisRow.Add(totalElementsChecked + 1 - previousAddedElements);
                        previousAddedElements = totalElementsChecked + 1;
                    }
                    totalElementsChecked++;
                }
                //add final split element. add up all other splits and make last one reach max
                int sum = 0;
                foreach (int thisInt in splitForThisRow)
                {
                    sum = sum + thisInt;
                }
                int amountLeft = thisRow.IDList.Count() - sum;
                splitForThisRow.Add(amountLeft);
                //update previous Added Elements
                previousAddedElements = totalElementsChecked;
                IDWithList newRow = new IDWithList(thisRow.ID, splitForThisRow);
                basketSplitTable.Add(newRow);
                //add row to listToREturn
            }
            return basketSplitTable;
        }
        private void createProductionTable()
        {
            productionTable = new List<IDWithList>();
            List<bUUDataObject> unitList = pairObjectLibrary.Where(x => !x.isBuilding).ToList();
            foreach (bUUDataObject thisObject in pairObjectLibrary)
            {
                if (thisObject.isBuilding && thisObject.supplyProvided == 0)//don't include nexus
                {
                    //find all units produced out of this
                    List<int> unitIDs = new List<int>();
                    foreach (bUUDataObject thisUnit in unitList)
                    {
                        if (thisUnit.producedOutOf.Equals(thisObject.name) && thisUnit.mineralCost>0 ) //dont include WG
                        {
                            unitIDs.Add(thisUnit.bUUDataID);
                        }
                    }
                    if (unitIDs.Count() > 0)
                    {
                        IDWithList newEntry = new IDWithList(thisObject.bUUDataID, unitIDs);
                        productionTable.Add(newEntry);
                    }
                }
            }
        }
    }
}

