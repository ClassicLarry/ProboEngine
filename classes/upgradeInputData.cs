using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class upgradeInputData
    {
        //forge
        public bool groundWeapons1 { get; set; }
        public bool groundWeapons2 { get; set; }
        public bool groundWeapons3 { get; set; }
        public bool groundArmor1 { get; set; }
        public bool groundArmor2 { get; set; }
        public bool groundArmor3 { get; set; }
        public bool shields1 { get; set; }
        public bool shields2 { get; set; }
        public bool shields3 { get; set; }
        public bool warpGateResearch { get; set; }

        //cyberCore
        public bool airWeapons1 { get; set; }
        public bool airWeapons2 { get; set; }
        public bool airWeapons3 { get; set; }
        public bool airArmor1 { get; set; }
        public bool airArmor2 { get; set; }
        public bool airArmor3 { get; set; }

        //twilightCouncil
        public bool charge { get; set; }
        public bool blink { get; set; }
        public bool resonatingGlaives { get; set; }

        //supportBay
        public bool graviticBoosters { get; set; }
        public bool graviticDrive { get; set; }
        public bool extendedThermalLance { get; set; }

        //fleetBeacon
        public bool anionPulseCrystals { get; set; }
        public bool gravitonCatapult { get; set; }
        
        //templarArchives
        public bool psionicStorm { get; set; }

        //darkShrine
        public bool shadowStride { get; set; }

        public upgradeInputData(bool myGroundWeapons1, bool myGroundWeapons2, bool myGroundWeapons3, bool myGroundArmor1, bool myGroundArmor2, bool myGroundArmor3, bool myShields1, bool myShields2, bool myShields3,
            bool mywarpGateResearch, bool myAirWeapons1, bool myAirWeapons2, bool myAirWeapons3, bool myAirArmor1, bool myAirArmor2, bool myAirArmor3, bool myCharge, bool myBlink, bool myResonatingGlaives, bool myGraviticBoosters,
            bool myGraviticDrive, bool myExtendedThermalLance, bool myAnionPulseCrystals, bool myGravitonCatapult, bool myPsionicStorm, bool myShadowStride)
        {
            //forge
            groundWeapons1 = myGroundWeapons1;
            groundWeapons2 = myGroundWeapons2;
            groundWeapons3 = myGroundWeapons3;
            groundArmor1 = myGroundArmor1;
            groundArmor2 = myGroundArmor2;
            groundArmor3 = myGroundArmor3;
            shields1 = myShields1;
            shields2 = myShields2;
            shields3 = myShields3;

            //cyberCore
            warpGateResearch = mywarpGateResearch;
            airWeapons1 = myAirWeapons1;
            airWeapons2 = myAirWeapons2;
            airWeapons3 = myAirWeapons3;
            airArmor1 = myAirArmor1;
            airArmor2 = myAirArmor2;
            airArmor3 = myAirArmor3;

            //twilightCouncil
            charge = myCharge;
            blink = myBlink;
            resonatingGlaives = myResonatingGlaives;

            //supportBay
            graviticBoosters = myGraviticBoosters;
            graviticDrive = myGraviticDrive;
            extendedThermalLance = myExtendedThermalLance;

            //fleetBeacon
            anionPulseCrystals = myAnionPulseCrystals;
            gravitonCatapult = myGravitonCatapult;

            //templarArchives
            psionicStorm = myPsionicStorm;

            //dark shrine
            shadowStride = myShadowStride;
        
        }
    }
}
