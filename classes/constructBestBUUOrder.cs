using System;
using System.Collections.Generic;
using System.Linq;

namespace ProboEngine_Stand_Alone_Version
{
	public class constructBestBUUOrder
    {
        public bool discardBuild { get; set; }
        private List<bUUEvent> eventList { get; set; }
        private List<PairsTable> pairList { get; set; }
        public List<bUUDataObject> pairObjectLibrary { get; set; }
        private List<IDWithList> lastBasketEntries { get; set; }
        private List<IDWithList> precedenceRelations { get; set; }
        private List<double> approximateChronoTimes { get; set; }
        private List<bUUEvent> orderedEventList { get; set; }
        public List<int> WGREventChain { get; set; }
        private bool WGIterationDone { get; set; }
        public double excessMinerals { get; set; }
        public double excessGas { get; set; }

        public List<bUUEvent> bestBuild { get; set; }
        public chronoOptionEvaluation bestChronoOptionEvaluation { get; set; }

        private List<bUUEvent> cchronoEventList { get; set; }
        private List<bUUEvent> semiOrderedChronoEventList { get; set; } //use this for all ordered lists so aren't creating a new space in memory
        public int numChronoFunctionsRan { get; set; }

        public constructBestBUUOrder(double globalFastestBuildYet, int WGRSpot, List<bUUDataObject> myPairObjectLibrary, List<IDWithList> myProductionTable, List<IDWithList> myBasketSplitTable, List<bUUEvent> myEventList, List<PairsTable> myPairList, List<IDWithList> myPrecedenceRelations, List<double> emptyAlteredMineralStatements, List<double> mineralStatements, List<double> gasStatements, List<double> myApproximateChronoTimes, int globalIterationNum, bool firstChronoSelected)
        {
            numChronoFunctionsRan = 0;
            WGREventChain = new List<int>();
            approximateChronoTimes = myApproximateChronoTimes;
            precedenceRelations = myPrecedenceRelations;
            createNewVersions(myEventList, myPairList, myPairObjectLibrary);

            //1. go through each row in basketSplitTable to create events and pairs for all units
            createUUEventsAndPairs(myProductionTable, myBasketSplitTable);
            if (discardBuild) { return; }
            //2. Add in secondary buildings
            addSecondaryBuildings();
            //3. Add in primary buildings at latest possible time given precedence constraints
            addPrimaryBuildings();
            //3.5 Update WGR times to be directly made once cyber is done
            if (WGRSpot >= 0) {
				updateWGRTime(WGRSpot);
			}
            //adding names to each event for debugging purposes
            addNamesToEvents();
			if (WGRSpot >= 0)
			{
				bUUEvent wgr = eventList.First(x => x.name == "warpGateResearch");
				wgr.startTime = wgr.startTime - 0.01;
			}


			orderedEventList = new List<bUUEvent>(eventList.OrderBy(x => x.startTime));

            if (orderedEventList[0].startTime < -500) { discardBuild = true; return; }

            //4. Left shift everything as a group based on nonrenewable resource chart
            double timeShifted = leftShiftEntireBuild(mineralStatements, gasStatements);
            if (discardBuild) { return; } //discard if time shifted makes over 500 long

            //cut build if timeShifted>globalFastestBuildYet*1.3
            if (timeShifted > globalFastestBuildYet * 1.3)
            { discardBuild = true; return; }

            //5. Consider how many available chronos based on current time ETA. Repeat below steps for each combination of chronos
            if (timeShifted < globalFastestBuildYet) { globalFastestBuildYet = timeShifted; }
            List<List<int>> chronoOptions = createChronoOptions(globalFastestBuildYet, WGRSpot, globalIterationNum, firstChronoSelected);

            if (WGRSpot >= 0)
            {
                int WGREventID = pairObjectLibrary[WGRSpot].bUUEventIDForPrimaryBuilding;
                int cyberCoreEventID = pairList[eventList[WGREventID].pairAsChildID].parentBUUEventID;
                int gatewayEventID = pairList[eventList[cyberCoreEventID].pairAsChildID].parentBUUEventID;
                int pylonEventID = pairList[eventList[gatewayEventID].pairAsChildID].parentBUUEventID;
                WGREventChain = new List<int> { pylonEventID, gatewayEventID, cyberCoreEventID, WGREventID };
            }

            //create a new eventList that will be referenced by each chronoOption (allows to edit start and endtimes while still keeping parent reference
            createEventListCopy(); //initializes cchronoEventList and bestBuild
            semiOrderedChronoEventList = new List<bUUEvent>(cchronoEventList.OrderBy(x => x.startTime));

			//test each chrono option
            for (int i = 0; i < chronoOptions.Count(); i++)
            {
                chronoOptionEvaluation newChronoOptionEvaluation = new chronoOptionEvaluation(globalFastestBuildYet, i, chronoOptions[i], mineralStatements, gasStatements, emptyAlteredMineralStatements, eventList, orderedEventList, cchronoEventList, semiOrderedChronoEventList, WGRSpot, pairObjectLibrary, approximateChronoTimes, WGREventChain, lastBasketEntries, precedenceRelations, pairList, globalIterationNum, firstChronoSelected);
                numChronoFunctionsRan++;
                // first chrono option is all chains ends. If this option is not within 10 % of bestYet, dont test any other chrono options.
                if (newChronoOptionEvaluation.buildEndTime > 1.1 * globalFastestBuildYet && i == 0)
                {
                    break;
                }
                //check if evaluation is new best 
                if (newChronoOptionEvaluation.buildEndTime < globalFastestBuildYet || newChronoOptionEvaluation.buildEndTime == globalFastestBuildYet && newChronoOptionEvaluation.chronoEcoLog.Count() == 0)
                {
                    globalFastestBuildYet = newChronoOptionEvaluation.buildEndTime;
                    bestChronoOptionEvaluation = newChronoOptionEvaluation;
                    excessMinerals = findExcessMinerals(mineralStatements, newChronoOptionEvaluation.buildEndTime);
                    excessGas = findExcessGas(gasStatements, newChronoOptionEvaluation.buildEndTime);

                    //the chronoEventList times will get altered after each run. Need to save a copy of the best runTimes
                    updateBestChronoEventList(newChronoOptionEvaluation.chronoEventList);

                }
            }
            //if no build is faster than globalFastestBuildYet
            if (bestChronoOptionEvaluation == null) { discardBuild = true; }
        }
        private void updateBestChronoEventList(List<bUUEvent> newBestChronoEventList)
        {
            //need to make sure bestBuild has the right number of warpgate events
            for (int i = 0; i < newBestChronoEventList.Count(); i++)
            {
                if (i < bestBuild.Count())
                {
                    bestBuild[i].startTime = newBestChronoEventList[i].startTime;
                    bestBuild[i].endTime = newBestChronoEventList[i].endTime;
                    bestBuild[i].fromWG = newBestChronoEventList[i].fromWG;
                    bestBuild[i].pairAsChildID = newBestChronoEventList[i].pairAsChildID;
                    bestBuild[i].pairAsParentID = newBestChronoEventList[i].pairAsParentID;
                    bestBuild[i].bUUDataID = newBestChronoEventList[i].bUUDataID;
                }
                else
                {
                    //need to add on new event to bestBuild
                    bUUEvent newEvent = new bUUEvent(newBestChronoEventList[i].bUUEventID, newBestChronoEventList[i].startTime, newBestChronoEventList[i].endTime, newBestChronoEventList[i].bUUDataID, newBestChronoEventList[i].pairAsChildID, newBestChronoEventList[i].pairAsParentID, newBestChronoEventList[i].name);
                    bestBuild.Add(newEvent);
                }
            }
            //remove any extra items in bestBuild
            int extraEvents = bestBuild.Count() - newBestChronoEventList.Count();
            for (int i = 0; i < extraEvents; i++)
            {
                int spot = bestBuild.Count() - 1 - i;
                bestBuild.RemoveAt(spot);
            }
        }
        private void addNamesToEvents()
        {
            foreach (bUUEvent thisEvent in eventList)
            {
                thisEvent.name = pairObjectLibrary[thisEvent.bUUDataID].name;
            }
        }

        private void createEventListCopy()
        {
            cchronoEventList = new List<bUUEvent>();
            bestBuild = new List<bUUEvent>();
            foreach (bUUEvent thisEvent in eventList)
            {
                bUUEvent newEvent = new bUUEvent(thisEvent.bUUEventID, thisEvent.startTime, thisEvent.endTime, thisEvent.bUUDataID, thisEvent.pairAsChildID, thisEvent.pairAsParentID, thisEvent.name);
                cchronoEventList.Add(newEvent);
                bUUEvent newEvent2 = new bUUEvent(thisEvent.bUUEventID, thisEvent.startTime, thisEvent.endTime, thisEvent.bUUDataID, thisEvent.pairAsChildID, thisEvent.pairAsParentID, thisEvent.name);
                bestBuild.Add(newEvent2);
            }
        }

        private void createNewVersions(List<bUUEvent> myEventList, List<PairsTable> myPairList, List<bUUDataObject> myPairObjectLibrary)
        {
            eventList = new List<bUUEvent>();
            pairList = new List<PairsTable>();
            pairObjectLibrary = new List<bUUDataObject>();
            foreach (bUUEvent thisEvent in myEventList)
            {
                bUUEvent newEvent = new bUUEvent(thisEvent.bUUEventID, thisEvent.startTime, thisEvent.endTime, thisEvent.bUUDataID, thisEvent.pairAsChildID, thisEvent.pairAsParentID);
                eventList.Add(newEvent);
            }
            foreach (PairsTable thisPair in myPairList)
            {
                PairsTable newPair = new PairsTable(thisPair.pairID, thisPair.parentBUUEventID, thisPair.childBUUEventID);
                pairList.Add(newPair);
            }
            foreach (bUUDataObject thisObject in myPairObjectLibrary)
            {
                bUUDataObject newObject = new bUUDataObject(thisObject.bUUDataID, thisObject.bUUEventIDForPrimaryBuilding, thisObject.name, thisObject.duration, thisObject.durationWithwarpGateResearch, thisObject.mineralCost, thisObject.gasCost, thisObject.isProductionBuilding, thisObject.isUnit, thisObject.isBuilding, thisObject.producedOutOf, thisObject.buildingReqList, thisObject.supplyCost, thisObject.supplyProvided);
                pairObjectLibrary.Add(newObject);
            }
        }
        private double findExcessMinerals(List<double> mineralStatements, double thisEndTime)
        {
            int endTime = (int)Math.Ceiling(thisEndTime);
            int totalMineralsSpent = 0;
            for (int i = 0; i < cchronoEventList.Count(); i++)
            {
                totalMineralsSpent = totalMineralsSpent + pairObjectLibrary[cchronoEventList[i].bUUDataID].mineralCost;
            }
            return mineralStatements[endTime] - totalMineralsSpent;
        }
        private double findExcessGas(List<double> gasStatements, double thisEndTime)
        {
            int endTime = (int)Math.Ceiling(thisEndTime);
            int totalGasSpent = 0;
            for (int i = 0; i < cchronoEventList.Count(); i++)
            {
                totalGasSpent = totalGasSpent + pairObjectLibrary[cchronoEventList[i].bUUDataID].gasCost;
            }
            return gasStatements[endTime] - totalGasSpent;
        }


        private void updateWGRTime(int WGRSpot)
        {
            int wgrEventID = pairObjectLibrary[WGRSpot].bUUEventIDForPrimaryBuilding;
            int cyberEventID = pairList[eventList[wgrEventID].pairAsChildID].parentBUUEventID;
            double wgrDuration = eventList[wgrEventID].endTime - eventList[wgrEventID].startTime;
            eventList[wgrEventID].startTime = eventList[cyberEventID].endTime;
            eventList[wgrEventID].endTime = eventList[wgrEventID].startTime + wgrDuration;
        }
        private List<List<int>> createChronoOptions(double timeShifted, int WGRSpot, int globalIterationNum, bool firstChronoSelected)
        {
            List<List<int>> differentChronoDirections = new List<List<int>>();
            //for now only allow first eco chrono at 36 seconds when pylon done
            //create a list of (List of ints describing chrono order)
            //0=eco, 1=WGR, 2=CE

            //rule of chrono branching:
            //1. Once chain ends has been selected, it becomes the only option
            //2. Once WGR is selected, Eco cannot be selected until WGR is done
            //3. WGR can only be selected if WGR is not done (Since WGR completion time is not fully known, certain chrono options may be deemed invalid later)
            //4. For now use a conservative "maxWGR done time" of 110% of when wgr time will be done given current position

            //5. Dont chrono Eco within 120 seconds of timeShifted
            //if WGR is never used, treat endTime as t=0;
            //find when WGR is done
            double approxWGREndTime = 0;
            if (WGRSpot >= 0)
            {
                double wgrEndTime = eventList[pairObjectLibrary[WGRSpot].bUUEventIDForPrimaryBuilding].endTime + timeShifted;
                approxWGREndTime = wgrEndTime * 1.1;
            }

            //step through approximateChronoTimes

            //at each time, first check if time is before timeShifted. If so, create new branches
            for (int i = 0; i < approximateChronoTimes.Count(); i++)
            {
                //break if next chrono is after build is already done
                if (approximateChronoTimes[i] > timeShifted) { break; }
                List<List<int>> newChronoBranches = new List<List<int>>();
                if (i == 0) //different on first iteration
                {
                    //if globalIterationNUm>0 && firstChronoSelected,delete first chrono option (already set on eco)
                    if (globalIterationNum == 0 || !firstChronoSelected)
                    {
                        List<int> ecoOption = new List<int>();
                        ecoOption.Add(0);
                        newChronoBranches.Add(ecoOption);
                        List<int> ceOption = new List<int>();
                        ceOption.Add(2);
                        newChronoBranches.Add(ceOption);
                        if (approxWGREndTime > approximateChronoTimes[0])
                        {
                            List<int> wgrOption = new List<int>();
                            wgrOption.Add(1);
                            newChronoBranches.Add(wgrOption);
                        }
                    }
                    else if (approximateChronoTimes.Count() > 1)//if deleting first chrono (using on eco), but still need to create branches with second chrono
                    {
                        List<int> ceOption = new List<int>();
                        ceOption.Add(2);
                        newChronoBranches.Add(ceOption);
                        if (approxWGREndTime > approximateChronoTimes[1])
                        {
                            List<int> wgrOption = new List<int>();
                            wgrOption.Add(1);
                            newChronoBranches.Add(wgrOption);
                        }
                        i++;//add 1 to i to skip first chrono (already commited to eco)
                    }

                }
                else
                {
                    foreach (List<int> thisList in differentChronoDirections)
                    {
                        List<List<int>> newLists = createNewChronoBranches(thisList, approxWGREndTime, approximateChronoTimes[i], timeShifted);
                        newChronoBranches.AddRange(newLists);
                    }
                }
                differentChronoDirections = newChronoBranches;
            }
            //find copy with all CE and move to front of line 

            int all2Spot = 0;
            for (int i = 0; i < differentChronoDirections.Count(); i++)
            {
                bool all2 = true;
                //check if all entries are 2
                for (int x = 0; x < differentChronoDirections[i].Count(); x++)
                {
                    if (differentChronoDirections[i][x] != 2) { all2 = false; }
                }
                if (all2) { all2Spot = i; break; }
            }
            differentChronoDirections.Insert(0, differentChronoDirections[all2Spot]);
            differentChronoDirections.RemoveAt(all2Spot + 1);
            return differentChronoDirections;
        }

        private List<List<int>> createNewChronoBranches(List<int> thisList, double approxWGREndTime, double newChronoTime, double buildEndTime)
        {
            //for now disable eco chronos after first one done in above function

            //dont allow eco chronos after the 6th chrono or within 2 minutes of end of build
            double maxTimeForEcoChrono = buildEndTime - 120;
            List<List<int>> newChronoBranches = new List<List<int>>();
            //0=eco, 1=WGR, 2=CE
            //rule of chrono branching:
            //1. Once chain ends has been selected, it becomes the only option
            //2. Once WGR is selected, Eco cannot be selected until WGR is done
            //3. WGR can only be selected if WGR is not done (Since WGR completion time is not fully known, certain chrono options may be deemed invalid later)
            //4. For now use a conservative "maxWGR done time" of 110% of when wgr time will be done given current position
            //5. Only allow a maximum of 4 chronos on WG (Each chrono takes up 30 seconds of WGR, total time is 114 seconds)
            //return any additional lists that have been created
            bool addWGRChrono = false;
            bool addEcoChrono = false;
            if (thisList.Last() == 1)
            {
                //if WGR is not done, only WGR and CE are options
                if (newChronoTime < approxWGREndTime)
                {
                    addWGRChrono = true;
                }
                else { addEcoChrono = true; }
            }
            else if (thisList.Last() == 0)
            {
                //Eco and CE are options
                addEcoChrono = true;
                //WGR is option if WGR is not done
                if (newChronoTime < approxWGREndTime) { addWGRChrono = true; }
            }
            if (addWGRChrono)
            {
                //check if last 4 chronos have all been on WG
                if (last4ChronosNotAllOnWG(thisList))
                {
                    List<int> newList = new List<int>(thisList);
                    newList.Add(1);
                    newChronoBranches.Add(newList);
                }

            }
            if (addEcoChrono)
            {
                //only add if not in last 120 seconds and in first 6 elements
                if (newChronoTime < maxTimeForEcoChrono && thisList.Count < 6)
                {
                    List<int> newList = new List<int>(thisList);
                    newList.Add(0);
                    //REMOVED FOR NOW TO DISABLE ECO CHRONO
                    //newChronoBranches.Add(newList);
                }
            }
            //edit existing List
            thisList.Add(2);
            newChronoBranches.Add(thisList);
            return newChronoBranches;
        }
        private bool last4ChronosNotAllOnWG(List<int> thisList)
        {
            //if last 4 entries are 1 (for chrono WG), return false
            //else, return true;
            bool last4AllWG = true;
            if (thisList.Count() < 4) { return true; }
            else
            {
                //check last 4 elements
                for (int i = thisList.Count() - 1; i >= thisList.Count() - 4; i--)
                {
                    if (i != 1) { return true; }
                }
            }
            return false;
        }
        private double leftShiftEntireBuild(List<double> mineralStatements, List<double> gasStatements)
        {
            int totalMineralsSpent = 0;
            int totalGasSpent = 0;
            //go through entire event list, shift events until meet criteria
            //first order eventList by startTime
            //do initial shift to where all eventTimes are >=0
            double timeShifted = -orderedEventList[0].startTime;
            foreach (bUUEvent thisEvent in orderedEventList)
            {
                //update totalMinerals and gas spent
                totalMineralsSpent = totalMineralsSpent + pairObjectLibrary[thisEvent.bUUDataID].mineralCost;
                totalGasSpent = totalGasSpent + pairObjectLibrary[thisEvent.bUUDataID].gasCost;
                //check if can afford at startTime+timeShifted
                //if not, increase timeShifted
                //timeShifted = timeShifted + findTimeUntilAfford(totalMineralsSpent, totalGasSpent, thisEvent.startTime, timeShifted, mineralStatements, gasStatements);
                timeShifted = timeShifted + findTimeUntilAfford(totalMineralsSpent, totalGasSpent, thisEvent.startTime, timeShifted, mineralStatements, gasStatements);
                if (timeShifted + orderedEventList[0].startTime >= 500) { discardBuild = true; break; }
            }
            return timeShifted;
        }
        private double findTimeUntilAfford(int totalMineralsSpent, int totalGasSpent, double eventStartTime, double timeShifted, List<double> mineralStatements, List<double> gasStatements)
        {
            //first check if condition is met at currentTime
            int nearestTime = (int)(eventStartTime + timeShifted);
            if (nearestTime >= 500) { return nearestTime; } //outside 500 bound, return dummy val
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
                //return 500 as dummy val if outside 500 bound
                if (val >= 500) { return 500; }

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

        private void addPrimaryBuildings()
        {
            //go backwards through precedence list
            //each building endTime is set to minimum startTime of all events in precedence contraints, and last basket entry for this object)
            for (int i = precedenceRelations.Count() - 1; i >= 0; i--)
            {
                double minEndTime = 100000;
                int thisEventID = precedenceRelations[i].ID;
                int thisBUUID = eventList[thisEventID].bUUDataID;
                //first check precedence items
                foreach (int unitEventID in precedenceRelations[i].IDList)
                {
                    if (eventList[unitEventID].startTime < minEndTime)
                    {
                        minEndTime = eventList[unitEventID].startTime;
                    }
                }
                //next check last basket entry
                List<IDWithList> lastUnit = lastBasketEntries.Where(x => x.ID == thisBUUID).ToList();
                int lastEventID = -1;
                if (lastUnit.Any())
                {
                    //will only have 1 entry (check first item in IDList because this will be primary basket)
                    lastEventID = lastUnit[0].IDList[0];
                    if (eventList[lastEventID].startTime < minEndTime)
                    {
                        minEndTime = eventList[lastEventID].startTime;
                    }
                }

                //add event at minEndTime
                double duration = pairObjectLibrary[eventList[thisEventID].bUUDataID].duration;
                eventList[thisEventID].endTime = minEndTime;
                eventList[thisEventID].startTime = minEndTime - duration;
                //add pair between event and unit if unit was in basket
                if (lastEventID >= 0)
                {
                    PairsTable newPair = new PairsTable(pairList.Count(), thisEventID, lastEventID);
                    eventList[lastEventID].pairAsChildID = pairList.Count();
                    eventList[thisEventID].pairAsParentID = pairList.Count();
                    pairList.Add(newPair);
                }
            }
        }
        private void addSecondaryBuildings()
        {
            //go through lastBasketEntries
            foreach (IDWithList thisRow in lastBasketEntries)
            {
                //only consider entries after the first one
                for (int i = 1; i < thisRow.IDList.Count; i++)
                {
                    //add secondary building before this event
                    //find event production building to add correct secondary building
                    int childEventID = thisRow.IDList[i];
                    bUUDataObject B1 = pairObjectLibrary[thisRow.ID];
                    //need to create a new object that copies B1, but with new ID 
                    bUUDataObject newObject = new bUUDataObject(B1.name, B1.duration, B1.mineralCost, B1.gasCost, B1.isProductionBuilding, B1.isUnit, B1.supplyProvided, B1.isBuilding);
                    int dataID = pairObjectLibrary.Count();
                    newObject.bUUDataID = dataID;
                    pairObjectLibrary.Add(newObject);

                    //create new event
                    int eventID = eventList.Count();
                    double endTime = eventList[childEventID].startTime;
                    double duration = B1.duration;
                    bUUEvent newEvent = new bUUEvent(eventID, endTime - duration, endTime, dataID, pairList.Count() + 1, pairList.Count());
                    eventList.Add(newEvent);
                    //create new pair
                    PairsTable newPair = new PairsTable(pairList.Count(), eventID, childEventID);
                    pairList.Add(newPair);

                    //also need to create pair connected secondaryBuilding to its parent
                    //first find parent
                    int parentOfSecondaryBuildingEventID = pairList[eventList[pairObjectLibrary[thisRow.ID].bUUEventIDForPrimaryBuilding].pairAsChildID].parentBUUEventID;
                    PairsTable newPair2 = new PairsTable(pairList.Count(), parentOfSecondaryBuildingEventID, eventID);
                    pairList.Add(newPair2);
                    //parentEvent will not have relationship added for this pair (only has space to show one relationship currently)

                    //update event PairAsChildID of child
                    eventList[childEventID].pairAsChildID = pairList.Count() - 2;
                }
            }
        }
        private void createUUEventsAndPairs(List<IDWithList> productionTable, List<IDWithList> basketSplitTable)
        {
            lastBasketEntries = new List<IDWithList>();
            // Split each row in productionTable according to basketSplitTable
            for (int i = 0; i < productionTable.Count(); i++)
            {
                //make sure upgrades always added to predecesors basket
                if (pairObjectLibrary[productionTable[i].ID].name == "forge" && productionTable[i].IDList.Count() > 1 && basketSplitTable[i].IDList.Count() > 1)
                {
                    productionTable[i].IDList = sortForgeList(productionTable[i].IDList, basketSplitTable[i].IDList);
                    if (discardBuild) { return; }
                }
                else if (pairObjectLibrary[productionTable[i].ID].name == "cyberCore" && productionTable[i].IDList.Count() > 1)
                {
                    //productionTable[i].IDList = sortCyberList(productionTable[i].IDList, basketSplitTable[i].IDList);
                }

                List<int> IDOfLastBasketEntry = new List<int>();
                IDWithList thisItem = new IDWithList(productionTable[i].ID, IDOfLastBasketEntry);
                lastBasketEntries.Add(thisItem);
                int nextBasket = 0;
                int numBaskets = basketSplitTable[i].IDList.Count();
                List<int> basketSpots = new List<int>(basketSplitTable[i].IDList);
                //need to keep a running total of last unit added to each bucket

                //go through every id in the row to fill baskets
                //go through list backwards to have higher tech units be added later
                bool noBasketsEmpty = false;
                for (int g = productionTable[i].IDList.Count() - 1; g >= 0; g--)
                {
                    //add this unit to next available basket (reduce basketSpots)
                    basketSpots[nextBasket]--;
                    int parentBUUID = productionTable[i].IDList[g];
                    int parentEventID = pairObjectLibrary[parentBUUID].bUUEventIDForPrimaryBuilding;
                    if (parentEventID == 0)
                    {
                        int pause = 0;
                    }
                    double endTime;
                    if (noBasketsEmpty)
                    {
                        //find child
                        int childEventID = lastBasketEntries[i].IDList[nextBasket];

                        //update event properties (endTime = startTime of child)
                        endTime = eventList[childEventID].startTime;
                        eventList[parentEventID].pairAsParentID = pairList.Count();
                        //add pair as parent (find child ID from IDOfLastBasketEntry)
                        PairsTable newPair = new PairsTable(pairList.Count(), parentEventID, childEventID);
                        pairList.Add(newPair);
                        //update child pair to account for new pair
                        eventList[childEventID].pairAsChildID = pairList.Count() - 1;
                    }
                    else
                    {
                        //first unit into basket (need to add spot in lastBasketEntries)
                        endTime = 0;
                        lastBasketEntries[i].IDList.Add(-1); //add -1 as placeholder                        
                    }

                    double duration = pairObjectLibrary[parentBUUID].duration;
                    eventList[parentEventID].startTime = endTime - duration;
                    eventList[parentEventID].endTime = endTime;
                    //update last item in bucket
                    lastBasketEntries[i].IDList[nextBasket] = parentEventID;
                    //update nextBasket
                    int currentBasket = nextBasket;
                    nextBasket = findNextBasket(nextBasket, basketSpots, numBaskets);
                    if (nextBasket <= currentBasket) { noBasketsEmpty = true; }

                }
            }
        }
        private List<int> sortForgeList(List<int> upgrades, List<int> numPerBasket)
        {
            //make sure (g3,g2,g1), (a3,a2,a1),(s3,s2,s1) in same basket
            List<List<int>> groupByBasket = new List<List<int>>();
            List<List<int>> groupByUpgrade = new List<List<int>>();
            List<int> g = new List<int>();
            List<int> a = new List<int>();
            List<int> s = new List<int>();
            foreach (int i in upgrades)
            {
                if (pairObjectLibrary[i].name == "groundWeapons1" || pairObjectLibrary[i].name == "groundWeapons2" || pairObjectLibrary[i].name == "groundWeapons3")
                {
                    g.Add(i);
                }
                else if (pairObjectLibrary[i].name == "groundArmor1" || pairObjectLibrary[i].name == "groundArmor2" || pairObjectLibrary[i].name == "groundArmor3")
                {
                    a.Add(i);
                }
                else { s.Add(i); }
            }
            //add to groupbyUpgrade if not null
            if (g.Any()) { groupByUpgrade.Add(g); }
            if (a.Any()) { groupByUpgrade.Add(a); }
            if (s.Any()) { groupByUpgrade.Add(s); }

            int numBaskets = numPerBasket.Count();
            //find which upgrade had most entries
            int biggestUpgrade = 0;
            for (int i = 1; i < groupByUpgrade.Count(); i++)
            {
                if (groupByUpgrade[i].Count() > groupByUpgrade[biggestUpgrade].Count())
                {
                    biggestUpgrade = i;
                }
            }
            //go through each gruop and add to col with most spots
            int numRuns = groupByUpgrade.Count();
            for (int i = 0; i < numRuns; i++)
            {
                if (i == 0)
                {
                    //start with biggest upgrade
                    groupByBasket.Add(groupByUpgrade[biggestUpgrade]);
                    groupByUpgrade.RemoveAt(biggestUpgrade);
                }
                else if (i == 1)
                {
                    biggestUpgrade = 0;
                    //find new biggest (only two options at most)
                    if (groupByUpgrade.Count() > 1)
                    {
                        if (groupByUpgrade[1].Count() > groupByUpgrade[0].Count()) { biggestUpgrade = 1; }
                    }
                    //groupByBasket.Add(groupByUpgrade[biggestUpgrade]);
                    //still have two options. find biggest and add where possible

                    //check if second basket or first has more space
                    int firstBasketSpace = numPerBasket[0] - groupByBasket[0].Count();
                    int secondBasketSpace = 0;
                    if (numPerBasket.Count() > 1) { secondBasketSpace = numPerBasket[1]; }
                    if (firstBasketSpace > secondBasketSpace)
                    {
                        //add to first Basket, but sort by complexity
                        foreach (int item in groupByUpgrade[biggestUpgrade])
                        {
                            //dont sort for now
                            groupByBasket[0].Add(item);
                        }
                        groupByUpgrade.RemoveAt(biggestUpgrade);
                    }
                    else
                    {
                        //add to second basket
                        groupByBasket.Add(groupByUpgrade[biggestUpgrade]);
                        groupByUpgrade.RemoveAt(biggestUpgrade);
                    }
                }
                else
                {
                    //last upgrade in group by upgrade
                    //find basket with most spots
                    int basketMostSpots = 0;
                    int mostSpots = -100;
                    //if both went in first basket, lastupgrades must go in second basket
                    if (groupByBasket.Count() == 1) { basketMostSpots = 2; } //set to 2 artificially to run following code
                    else
                    {
                        for (int f = 0; f < numPerBasket.Count(); f++)
                        {
                            if (f == 2)
                            {
                                //third basket wont exist yet
                                if (numPerBasket[f] > mostSpots)
                                {
                                    mostSpots = numPerBasket[f];
                                    basketMostSpots = f;

                                }
                            }
                            else
                            {
                                if (numPerBasket[f] - groupByBasket[f].Count() > mostSpots)
                                {
                                    mostSpots = numPerBasket[f] - groupByBasket[f].Count();
                                    basketMostSpots = f;
                                }
                            }
                        }
                    }

                    //add final group to basketMostSpots
                    if (basketMostSpots == 2) { groupByBasket.Add(groupByUpgrade[0]); }
                    else
                    {
                        foreach (int item in groupByUpgrade[0])
                        {
                            //later sort by complexity
                            groupByBasket[basketMostSpots].Add(item);
                        }
                    }
                }
            }
            //now have groupByBasket how items should end up
            //check if oversized
            for (int p = 0; p < groupByBasket.Count(); p++)
            {
                if (groupByBasket[p].Count() > numPerBasket[p]) { discardBuild = true; return upgrades; }
            }
            List<int> toReturn = new List<int>();
            //go through each basket and add to list
            int nextBasket = 0;
            int nBaskets = numPerBasket.Count();
            for (int p = 0; p < upgrades.Count(); p++)
            {
                toReturn.Add(groupByBasket[nextBasket][0]);
                groupByBasket[nextBasket].RemoveAt(0);
                //update next basket
                nextBasket++;
                //if empty or out of bounds, go back to first basket
                if (nextBasket >= nBaskets || groupByBasket[nextBasket].Count() == 0) { nextBasket = 0; }
            }
            return toReturn;
        }
        private int findNextBasket(int currentBasket, List<int> basketSpots, int numBaskets)
        {
            int nextBasket = currentBasket;
            //repeat below process until found eligible basket
            for (int i = 0; i < 1000; i++)
            {
                nextBasket++;
                if (nextBasket >= numBaskets)
                {
                    nextBasket = 0;
                }
                if (basketSpots[nextBasket] > 0) { break; }
            }
            return nextBasket;
        }
    }
}
