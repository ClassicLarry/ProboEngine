using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class UpgradePackager
    {
        public List<string> forgeUpgrades { get; set; }
        public List<string> cyberCoreUpgrades { get; set; }
        public List<string> twilightCouncilUpgrades { get; set; }
        public List<string> supportBayUpgrades { get; set; }
        public List<string> fleetBeaconUpgrades { get; set; }
        public List<string> templarArchivesUpgrades { get; set; }
        public List<string> darkShrineUpgrades { get; set; }

        public List<Upgrade> allUpgrades { get; set; }

        public UpgradePackager(List<Upgrade> myForgeUpgrades, List<Upgrade> myCyberCoreUpgrades, List<Upgrade> myTwilightCouncilUpgrades, List<Upgrade> mySupportBayUpgrades, List<Upgrade> myFleetBeaconUpgrades,
            List<Upgrade> myTemplarArchivesUpgrades, List<Upgrade> myDarkShrineUpgrades)
        {
            //foreach each list downselect to only those where needed is true
            List<Upgrade> tempforgeUpgrades = myForgeUpgrades.Where(x => x.needed).ToList();
            List<Upgrade> tempcyberCoreUpgrades = myCyberCoreUpgrades.Where(x => x.needed).ToList();
            List<Upgrade> temptwilightCouncilUpgrades = myTwilightCouncilUpgrades.Where(x => x.needed).ToList();
            List<Upgrade> tempsupportBayUpgrades = mySupportBayUpgrades.Where(x => x.needed).ToList();
            List<Upgrade> tempfleetBeaconUpgrades = myFleetBeaconUpgrades.Where(x => x.needed).ToList();
            List<Upgrade> temptemplarArchivesUpgrades = myTemplarArchivesUpgrades.Where(x => x.needed).ToList();
            List<Upgrade> tempdarkShrineUpgrades = myDarkShrineUpgrades.Where(x => x.needed).ToList();

            //create string lists
            forgeUpgrades = tempforgeUpgrades.Select(x => x.name).ToList();
            cyberCoreUpgrades = tempcyberCoreUpgrades.Select(x => x.name).ToList();
            twilightCouncilUpgrades = temptwilightCouncilUpgrades.Select(x => x.name).ToList();
            supportBayUpgrades = tempsupportBayUpgrades.Select(x => x.name).ToList();
            fleetBeaconUpgrades = tempfleetBeaconUpgrades.Select(x => x.name).ToList();
            templarArchivesUpgrades = temptemplarArchivesUpgrades.Select(x => x.name).ToList();
            darkShrineUpgrades = tempdarkShrineUpgrades.Select(x => x.name).ToList();

            allUpgrades = new List<Upgrade>();
            allUpgrades.AddRange(tempforgeUpgrades);
            allUpgrades.AddRange(tempcyberCoreUpgrades);
            allUpgrades.AddRange(temptwilightCouncilUpgrades);
            allUpgrades.AddRange(tempsupportBayUpgrades);
            allUpgrades.AddRange(tempfleetBeaconUpgrades);
            allUpgrades.AddRange(temptemplarArchivesUpgrades);
            allUpgrades.AddRange(tempdarkShrineUpgrades);
        }
    }
}
