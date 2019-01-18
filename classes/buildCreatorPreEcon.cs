using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
    public class buildCreatorPreEcon
    {
        public double lastEventDuration { get; set; } //only found for the last unit of the fastest Build
        public List<buildOrderEntry> chronoWGLog { get; set; }
        public List<buildOrderEntry> chronoCELog { get; set; }
        public List<buildOrderEntry> chronoEcoLog { get; set; }
        public List<Unit> unitList { get; set; }
        public List<Building> buildingList { get; set; }
        public List<bUUDataObject> pairObjectsLibrary { get; set; }
        public List<bUUPairObject> buildingPairs { get; set; }
        public UpgradePackager upgradePackage { get; set; }

        public buildCreatorPreEcon(unitInputData newUnitInputData, upgradeInputData newUpgradeInputData)
        {
            //initialize units with game values and #needed (only add units needed)
            unitList = unitInitializer(newUnitInputData);

            //initialize builings with game values (add all buildings)
            buildingList = buildingInitializer();

            //initialize upgrades with game values (only add if upgrade is needed)
            upgradePackage = upgradeInitializer(newUpgradeInputData);

            //create building and unit objects with format to be used in pairs (also include upgrades)
            pairObjectsLibrary = initializeUnitBuildingPairObjects();

			//create parent/child list of buildings that need to be built
			buildingPairs = findTechTree();
		}

        public List<buildOrderEntry> createCompressedBuilds(List<BankStatement> freeCashFunction, List<double> approximateChronoTimes, double fastestYet, int globalIterationNum, bool firstChronoSelected)
        {
			List<buildOrderEntry> buildOrderNoEcon = new List<buildOrderEntry>();
			//find best build order based on cash and chrono availability. Pass in building and unit data.
			backwardsApproach thisApproach = new backwardsApproach(pairObjectsLibrary, buildingPairs, freeCashFunction, approximateChronoTimes, fastestYet,globalIterationNum,firstChronoSelected);

			//result may be null if no build was found under 500 seconds. If this occured, return a list with one item with start time of 100000.
			if (thisApproach.bestYet == null)
			{
				buildOrderEntry itemToAdd = new buildOrderEntry("Dummy", 100000);
				buildOrderNoEcon.Add(itemToAdd);
				return buildOrderNoEcon;
			}
			//else return formated build order
			return formatBuild(thisApproach, buildOrderNoEcon);
        }

		private List<buildOrderEntry> formatBuild(backwardsApproach thisApproach, List<buildOrderEntry> buildOrderNoEcon)
		{
			//need to pull start times of everything in order of occurence
			List<bUUEvent> orderedBuild = new List<bUUEvent>(thisApproach.bestYet.bestBuild.OrderBy(x => x.startTime));

			//build order needs a list of "bUUDataID, minStartTime"
			foreach (bUUEvent thisEvent in orderedBuild)
			{
				buildOrderEntry newOutput = new buildOrderEntry(thisApproach.bestYet.pairObjectLibrary[thisEvent.bUUDataID].name, thisEvent.startTime);
				buildOrderNoEcon.Add(newOutput);
			}

			//update last event duration
			lastEventDuration = orderedBuild[orderedBuild.Count() - 1].endTime - orderedBuild[orderedBuild.Count() - 1].startTime;

			//get chrono usage logs
			chronoWGLog = thisApproach.bestYet.bestChronoOptionEvaluation.chronoWGLog;
			chronoCELog = thisApproach.bestYet.bestChronoOptionEvaluation.chronoCELog;
			chronoEcoLog = thisApproach.bestYet.bestChronoOptionEvaluation.chronoEcoLog;

			return buildOrderNoEcon;
		}

		private List<bUUDataObject> initializeUnitBuildingPairObjects()
		{
			//create List of all possible building, upgrade, and units with pair details
			List<bUUDataObject> listToReturn = new List<bUUDataObject>();
			//first add God object
			bUUDataObject object1 = new bUUDataObject("godBuilding", 0, 0, 0, false, false, 0, true);
			listToReturn.Add(object1);

			//pull data from building initializer 
			foreach (Building thisBuilding in buildingList)
			{
				bUUDataObject newObject = new bUUDataObject(thisBuilding.name, thisBuilding.buildTime, thisBuilding.mineralCost, thisBuilding.gasCost, thisBuilding.isProductionBuilding, false, thisBuilding.supplyProvided, true);
				listToReturn.Add(newObject);
			}

			//pull data from upgrade initializer
			foreach (Upgrade thisUpgrade in upgradePackage.allUpgrades)
			{
				bUUDataObject newObject = new bUUDataObject(thisUpgrade.name, thisUpgrade.buildTime, thisUpgrade.mineralCost, thisUpgrade.gasCost, false, false, thisUpgrade.producedOutOf, thisUpgrade.techReqs, 0);
				listToReturn.Add(newObject);
			}

			//need a unique object for each instance of a unit
			foreach (Unit thisUnit in unitList)
			{
				for (int i = 0; i < thisUnit.numToBuild; i++)
				{
					string name = thisUnit.name + i.ToString();
					bUUDataObject newObject = new bUUDataObject(name, thisUnit.buildTime, thisUnit.buildTimeWithWG, thisUnit.mineralCost, thisUnit.gasCost, false, true, thisUnit.productionBuilding, thisUnit.highestTechBuildingList, thisUnit.supply);
					listToReturn.Add(newObject);
				}
			}
			//add in probe object and warpgate object(since number of probes will be set at 0)
			Unit probeStats = unitList.First(x => x.name.Equals("Probe"));
			bUUDataObject probeObject = new bUUDataObject(probeStats.name, probeStats.buildTime, probeStats.mineralCost, probeStats.gasCost, false, true, probeStats.productionBuilding, probeStats.highestTechBuildingList, probeStats.supply);
			listToReturn.Add(probeObject);
			Unit wGStats = unitList.First(x => x.name.Equals("warpGate"));
			bUUDataObject wGObject = new bUUDataObject(wGStats.name, wGStats.buildTime, wGStats.mineralCost, wGStats.gasCost, false, true, wGStats.productionBuilding, wGStats.highestTechBuildingList, wGStats.supply);
			listToReturn.Add(wGObject);

			//give each item an ID
			for (int i = 0; i < listToReturn.Count(); i++)
			{
				listToReturn[i].bUUDataID = i;
			}
			return listToReturn;
		}

		private static List<Unit> unitInitializer(unitInputData newInputData)
        {
            //include tech reqs for units that require extra buildings to be made
            Unit Probe = new Unit(12, 50, 0, 1, "nexus", 0, "Probe");
            Unit warpGate = new Unit(7, 0, 0, 0, "gateway", 0, "warpGate"); //requires warpgate research to be donef

            //include warpGateResearch times for gateway units
            Unit Zealot = new Unit(27, 100, 0, 2, "gateway", newInputData.numZealots, "Zealot", 20);
            Unit Stalker = new Unit(30, 125, 50, 2, "gateway", "cyberCore", newInputData.numStalkers, "Stalker", 23);
            Unit Sentry = new Unit(26, 50, 100, 2, "gateway", "cyberCore", newInputData.numSentries, "Sentry", 23);
            Unit Adept = new Unit(27, 100, 25, 2, "gateway", "cyberCore", newInputData.numAdepts, "Adept", 20);
            Unit HT = new Unit(39, 50, 150, 2, "gateway", "templarArchives", newInputData.numHT, "HT", 32);
            Unit DT = new Unit(39, 125, 125, 2, "gateway", "darkShrine", newInputData.numDT, "DT", 32);

            //robo units
            Unit Immortal = new Unit(39, 250, 100, 4, "roboticsFacility", newInputData.numImmortals, "Immortal");
            Unit Observer = new Unit(21, 25, 75, 1, "roboticsFacility", newInputData.numObservers, "Observer");
            Unit Collosus = new Unit(54, 300, 200, 6, "roboticsFacility", "supportBay", newInputData.numCollosi, "Collosus");
            Unit Disruptor = new Unit(36, 150, 150, 3, "roboticsFacility", "supportBay", newInputData.numDisruptors, "Disruptor");
            Unit Prism = new Unit(36, 200, 0, 2, "roboticsFacility", newInputData.numPrisms, "Prism"); 

            //stargate units
            Unit Oracle = new Unit(36, 150, 150, 3, "stargate", newInputData.numOracles, "Oracle");
            Unit Voidray = new Unit(43, 250, 150, 4, "stargate", newInputData.numVoidrays, "Voidray");
            Unit Carrier = new Unit(86, 350, 250, 6, "stargate", "fleetBeacon", newInputData.numCarriers, "Carrier");
            Unit Tempest = new Unit(43, 300, 200, 6, "stargate", "fleetBeacon", newInputData.numTempests, "Tempest");
            Unit Phoenix = new Unit(25, 150, 100, 2, "stargate", newInputData.numPhoenix, "Phoenix");

            //add units to list
            List<Unit> unitList = new List<Unit> { Probe, warpGate, Zealot, Stalker, Sentry, Adept, HT, DT, Prism, Immortal, Observer, Collosus, Disruptor, Oracle, Voidray, Phoenix, Carrier, Tempest, };
            return unitList;
        }

        private static List<Building> buildingInitializer()
        {
            Building nexus = new Building("nexus", 71, 420, 0, 15); //add 20 minerals due to travel to make new base
            Building assimilator = new Building("assimilator", 22, 75, 0, 0); //real duration is 21, adding 1 second to account for delay in when actual full on mining starts
            Building pylon = new Building("pylon", 18, 100, 0, 8);
            Building gateway = new Building("gateway", 46, 150, 0, true, 0);

            Building forge = new Building("forge", 32, 150, 0, 0);
            Building cyberCore = new Building("cyberCore", 36, 150, 0, 0);
            Building twilightCouncil = new Building("twilightCouncil", 36, 150, 100, 0);
            Building templarArchives = new Building("templarArchives", 36, 150, 200, 0);
            Building roboticsFacility = new Building("roboticsFacility", 46, 200, 100, true, 0);
            Building supportBay = new Building("supportBay", 46, 200, 200, 0);
            Building stargate = new Building("stargate", 43, 150, 150, true, 0);
            Building fleetBeacon = new Building("fleetBeacon", 43, 300, 200, 0);
            Building darkShrine = new Building("darkShrine", 71, 150, 150, 0);
            List<Building> buildingList = new List<Building> { nexus, assimilator, pylon, gateway, forge, cyberCore, twilightCouncil, templarArchives, roboticsFacility, supportBay, stargate, fleetBeacon, darkShrine };
            //add 10 minerals to each building to compensate mining time lost from probe producing building
            add10(buildingList);
            return buildingList;
        }
        private static void add10(List<Building> buildingList)
        {
            foreach (Building thisBuilding in buildingList)
            {
                thisBuilding.mineralCost = thisBuilding.mineralCost + 10;
            }
        }
        private static UpgradePackager upgradeInitializer(upgradeInputData newUpgradeInputData)
        {
            List<Upgrade> upgradeList = new List<Upgrade>();
            //upgrades from forge
            Upgrade groundWeapons1 = new Upgrade("groundWeapons1", "forge", 114, 100, 100, newUpgradeInputData.groundWeapons1);
            Upgrade groundArmor1 = new Upgrade("groundArmor1", "forge", 114, 100, 100, newUpgradeInputData.groundArmor1);
            Upgrade shields1 = new Upgrade("shields1", "forge", 114, 150, 150, newUpgradeInputData.shields1);
            Upgrade groundWeapons2 = new Upgrade("groundWeapons2", "forge", 136, 150, 150, "groundWeapons1", "twilightCouncil", newUpgradeInputData.groundWeapons2);
            Upgrade groundArmor2 = new Upgrade("groundArmor2", "forge", 136, 150, 150, "groundArmor1", "twilightCouncil", newUpgradeInputData.groundArmor2);
            Upgrade shields2 = new Upgrade("shields2", "forge", 136, 225, 225, "shields1", "twilightCouncil", newUpgradeInputData.shields2);
            Upgrade groundWeapons3 = new Upgrade("groundWeapons3", "forge", 157, 200, 200, "groundWeapons2", newUpgradeInputData.groundWeapons3);
            Upgrade groundArmor3 = new Upgrade("groundArmor3", "forge", 157, 200, 200, "groundArmor2", newUpgradeInputData.groundArmor3);
            Upgrade shields3 = new Upgrade("shields3", "forge", 157, 300, 300, "shields2", newUpgradeInputData.shields3);
            List<Upgrade> forgeUpgrades = new List<Upgrade>() { groundWeapons1, groundArmor1, shields1, groundWeapons2, groundArmor2, shields2, groundArmor3, groundWeapons3, shields3 };

            //upgrades from cyberCore
            Upgrade warpGateResearch = new Upgrade("warpGateResearch", "cyberCore", 114, 50, 50, newUpgradeInputData.warpGateResearch);
            Upgrade airWeapons1 = new Upgrade("airWeapons1", "cyberCore", 114, 100, 100, newUpgradeInputData.airWeapons1);
            Upgrade airArmor1 = new Upgrade("airArmor1", "cyberCore", 114, 150, 150, newUpgradeInputData.airArmor1);
            Upgrade airWeapons2 = new Upgrade("airWeapons2", "cyberCore", 136, 175, 175, "airWeapons1", "fleetBeacon", newUpgradeInputData.airWeapons2);
            Upgrade airArmor2 = new Upgrade("airArmor2", "cyberCore", 136, 225, 225, "airArmor1", "fleetBeacon", newUpgradeInputData.airArmor2);
            Upgrade airWeapons3 = new Upgrade("airWeapons3", "cyberCore", 157, 250, 250, "airWeapons2", newUpgradeInputData.airWeapons3);
            Upgrade airArmor3 = new Upgrade("airArmor3", "cyberCore", 157, 300, 300, "airArmor2", newUpgradeInputData.airArmor3);
            List<Upgrade> cyberCoreUpgrades = new List<Upgrade>() { warpGateResearch, airWeapons1, airArmor1, airWeapons2, airArmor2, airWeapons3, airArmor3 };

            //upgrades from twilightCouncil
            Upgrade charge = new Upgrade("charge", "twilightCouncil", 100, 100, 100, newUpgradeInputData.charge);
            Upgrade blink = new Upgrade("blink", "twilightCouncil", 121, 150, 150, newUpgradeInputData.blink);
            Upgrade resonatingGlaives = new Upgrade("resonatingGlaives", "twilightCouncil", 100, 100, 100, newUpgradeInputData.resonatingGlaives);
            List<Upgrade> twilightCouncilUpgrades = new List<Upgrade>() { charge, blink, resonatingGlaives };

            //upgrades from supportBay
            Upgrade graviticBoosters = new Upgrade("graviticBoosters", "supportBay", 57, 100, 100, newUpgradeInputData.graviticBoosters);
            Upgrade graviticDrive = new Upgrade("graviticDrive", "supportBay", 57, 100, 100, newUpgradeInputData.graviticDrive);
            Upgrade extendedThermalLance = new Upgrade("extendedThermalLance", "supportBay", 100, 150, 150, newUpgradeInputData.extendedThermalLance);
            List<Upgrade> supportBayUpgrades = new List<Upgrade>() { graviticBoosters, graviticDrive, extendedThermalLance };

            //upgrades from fleetBeacon
            Upgrade anionPulseCrystals = new Upgrade("anionPulseCrystals", "fleetBeacon", 64, 150, 150, newUpgradeInputData.anionPulseCrystals);
            Upgrade gravitonCatapult = new Upgrade("gravitonCatapult", "fleetBeacon", 57, 150, 150, newUpgradeInputData.gravitonCatapult);
            List<Upgrade> fleetBeaconUpgrades = new List<Upgrade>() { anionPulseCrystals, gravitonCatapult };

            //upgrades from templarArchives
            Upgrade psionicStorm = new Upgrade("psionicStorm", "templarArchives", 79, 200, 200, newUpgradeInputData.psionicStorm);
            List<Upgrade> templarArchivesUpgrades = new List<Upgrade>() { psionicStorm };

            //upgrades from darkShrine
            Upgrade shadowStride = new Upgrade("shadowStride", "darkShrine", 121, 100, 100, newUpgradeInputData.shadowStride);
            List<Upgrade> darkShrineUpgrades = new List<Upgrade>() { shadowStride };

            UpgradePackager newUpgradePackager = new UpgradePackager(forgeUpgrades, cyberCoreUpgrades, twilightCouncilUpgrades, supportBayUpgrades, fleetBeaconUpgrades, templarArchivesUpgrades, darkShrineUpgrades);
            return newUpgradePackager;
        }
        private List<bUUPairObject> findTechTree()
        {
            List<String> highestTechNeeded = new List<String>();
            List<bUUPairObject> ParentChildPairs = new List<bUUPairObject>();
            buildingsNeededClass buildingsNeeded = new buildingsNeededClass();
            bool tierTwoNeeded = false;
            foreach (Unit thisUnit in unitList)
            {
                foreach (string highestTechBuilding in thisUnit.highestTechBuildingList)
                {
                    //if highest tech building doesn't exist already in highestTechNeeded, add
                    if (!highestTechNeeded.Contains(highestTechBuilding) && thisUnit.numToBuild > 0)
                    {
                        highestTechNeeded.Add(highestTechBuilding);
                    }
                }
            }
            foreach (Upgrade thisUpgrade in upgradePackage.allUpgrades)
            {
                foreach (string highestTechBuilding in thisUpgrade.techReqs)
                {
                    //if highest tech building doesn't exist already in highestTechNeeded, add
                    if (!highestTechNeeded.Contains(highestTechBuilding))
                    {
                        highestTechNeeded.Add(highestTechBuilding);
                    }
                }
                //if highest tech building doesn't exist already in highestTechNeeded, add
                if (!highestTechNeeded.Contains(thisUpgrade.producedOutOf))
                {
                    highestTechNeeded.Add(thisUpgrade.producedOutOf);
                }

            }
            //check if highest tech needed. Since removed highest tech attribute for robo and stargate, checking those seperately
            if (highestTechNeeded.Contains("forge"))
            {
                buildingsNeeded.forge = true;
            }
            if (highestTechNeeded.Contains("twilightCouncil"))
            {
                buildingsNeeded.twilightCouncil = true;
                tierTwoNeeded = true;
            }
            if (highestTechNeeded.Contains("supportBay"))
            {
                buildingsNeeded.supportBay = true;
                buildingsNeeded.roboticsFacility = true;
                tierTwoNeeded = true;
            }
            else if (unitList.Where(x => x.productionBuilding.Equals("roboticsFacility")).ToList().Where(x => x.numToBuild > 0).ToList().Any())
            {
                buildingsNeeded.roboticsFacility = true;
                tierTwoNeeded = true;
            }
            if (highestTechNeeded.Contains("fleetBeacon"))
            {
                buildingsNeeded.fleetBeacon = true;
                buildingsNeeded.stargate = true;
                tierTwoNeeded = true;
            }
            else if (unitList.Where(x => x.productionBuilding.Equals("stargate")).ToList().Where(x => x.numToBuild > 0).ToList().Any())
            {
                buildingsNeeded.stargate = true;
                tierTwoNeeded = true;
            }
            if (highestTechNeeded.Contains("darkShrine"))
            {
                buildingsNeeded.darkShrine = true;
                buildingsNeeded.twilightCouncil = true;
                tierTwoNeeded = true;
            }
            if (highestTechNeeded.Contains("templarArchives"))
            {
                buildingsNeeded.templarArchives = true;
                buildingsNeeded.twilightCouncil = true;
                tierTwoNeeded = true;
            }
            if (tierTwoNeeded || highestTechNeeded.Contains("cyberCore"))
            {
                buildingsNeeded.cyberCore = true;
            }
            buildingsNeeded.gateway = true;

            //create all objects itemInPair to reference below if  true
            //use tiers to sort 
            //tier 0: pylon
            //tier 1: gateway, forge
            //tier 1.5: cyberCore
            //tier 2: robo, twi, stargate
            //tier 3: supportBay, templarArchives, darkShrine, fleetBeacon

            //add each pair to ParentChildPairs
            bUUPairObject pair0 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "godBuilding"), pairObjectsLibrary.First(x => x.name == "pylon"));
            ParentChildPairs.Add(pair0);
            if (buildingsNeeded.forge)
            {
                bUUPairObject pair1 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "pylon"), pairObjectsLibrary.First(x => x.name == "forge"));
                ParentChildPairs.Add(pair1);
            }
            if (buildingsNeeded.gateway)
            {
                bUUPairObject pair1 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "pylon"), pairObjectsLibrary.First(x => x.name == "gateway"));
                ParentChildPairs.Add(pair1);
            }
            if (buildingsNeeded.cyberCore)
            {
                bUUPairObject pair2 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "gateway"), pairObjectsLibrary.First(x => x.name == "cyberCore"));
                ParentChildPairs.Add(pair2);
            }
            if (buildingsNeeded.twilightCouncil)
            {
                bUUPairObject pair3 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "cyberCore"), pairObjectsLibrary.First(x => x.name == "twilightCouncil"));
                ParentChildPairs.Add(pair3);
            }
            if (buildingsNeeded.darkShrine)
            {
                bUUPairObject pair4 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "twilightCouncil"), pairObjectsLibrary.First(x => x.name == "darkShrine"));
                ParentChildPairs.Add(pair4);
            }
            if (buildingsNeeded.templarArchives)
            {
                bUUPairObject pair5 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "twilightCouncil"), pairObjectsLibrary.First(x => x.name == "templarArchives"));
                ParentChildPairs.Add(pair5);
            }
            if (buildingsNeeded.stargate)
            {
                bUUPairObject pair6 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "cyberCore"), pairObjectsLibrary.First(x => x.name == "stargate"));
                ParentChildPairs.Add(pair6);
            }
            if (buildingsNeeded.fleetBeacon)
            {
                bUUPairObject pair7 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "stargate"), pairObjectsLibrary.First(x => x.name == "fleetBeacon"));
                ParentChildPairs.Add(pair7);
            }
            if (buildingsNeeded.roboticsFacility)
            {
                bUUPairObject pair8 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "cyberCore"), pairObjectsLibrary.First(x => x.name == "roboticsFacility"));
                ParentChildPairs.Add(pair8);
            }
            if (buildingsNeeded.supportBay)
            {
                bUUPairObject pair9 = new bUUPairObject(pairObjectsLibrary.First(x => x.name == "roboticsFacility"), pairObjectsLibrary.First(x => x.name == "supportBay"));
                ParentChildPairs.Add(pair9);
            }

            return ParentChildPairs;
        }
    }
}
