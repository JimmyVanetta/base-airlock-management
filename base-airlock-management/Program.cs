using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        public static IMyBlockGroup habAirlock;
        public static List<IMyAirVent> habVents = new List<IMyAirVent>();
        public static List<IMyDoor> habDoors = new List<IMyDoor>();
        public static List<IMySensorBlock> habSensors = new List<IMySensorBlock>();
        public static List<MyDetectedEntityInfo> airlockEntities = new List<MyDetectedEntityInfo>();

        public static IMyBlockGroup hangarAirlock;
        public static List<IMyAirVent> hangarVents = new List<IMyAirVent>();
        public static List<IMyDoor> hangarDoors = new List<IMyDoor>();

        public static IMyDoor insideDoor;
        public static IMyDoor outsideDoor;
        public static IMySensorBlock habSensor;

        public int airlockStage = 0;
        public string airlockOxyLvl;
        public bool airlockCanPressurize = false;

        public string hangarOxyLvl;
        public bool hangarCanPressurize = false;


        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.a

            // fetch block groups - recompile to account for new blocks
            habAirlock = GridTerminalSystem.GetBlockGroupWithName("Hab Airlock");
            hangarAirlock = GridTerminalSystem.GetBlockGroupWithName("Hangar Airlock");

            // hab airlock doors, sensors and vents
            if (habAirlock != null)
            {
                habAirlock.GetBlocksOfType(habVents);
                habAirlock.GetBlocksOfType(habDoors);
                habAirlock.GetBlocksOfType(habSensors);

                if (habDoors.Count > 0)
                {
                    insideDoor = habDoors.First(d => d.CustomName.Contains("Inside"));
                    outsideDoor = habDoors.First(d => d.CustomName.Contains("Outside"));
                }
                else
                {
                    Echo("null habitat airlock door list");
                }

                if (habSensors.Count > 0)
                {
                    habSensor = habSensors.First(s => s.CustomName.Contains("Airlock"));
                }
            }
            else
            {
                Echo("null habitat airlock block group");
            }

            // hangar doors and vents
            if (hangarAirlock != null)
            {
                hangarAirlock.GetBlocksOfType(hangarDoors);
                hangarAirlock.GetBlocksOfType(hangarVents);
            } else
            {
                Echo("null hangar airlock block group");
            }

            // update every 10 ticks
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            // get oxygen level in airlock - we only need to see one vent oxy lvl to know the room is full.
            airlockOxyLvl = String.Format("{0:N2}", habVents[0].GetOxygenLevel());

            // get airlock pressurization status
            airlockCanPressurize = habVents[0].CanPressurize;

            // get detected players in airlock
            habSensor.DetectedEntities(airlockEntities);

            if (argument != "")
            {
                HandleUserInput(argument);
            }
            else
            {
                HandleAirlockStages();
            }
        }
        /// <summary>
        /// Handles airlock stages of operation
        /// </summary>
        public void HandleAirlockStages()
        {
            switch (airlockStage)
            {
                case 2:
                    HandleAirlockStageTwo();
                    break;

                case 3:
                    HandleAirlockStageThree();
                    break;

                // if airlock is not in use - this needs to be moved to a method of its own
                default:
                    HandleAirlockReset();
                    break;
            }
        }

        public void HandleAirlockStageTwo()
        {

            if (GoingIn())
            {
                Echo("Airlock ready to cycle");
            }
            else
            {
                if (airlockOxyLvl == "1.00")
                {
                    Echo("Airlock pressurized. Opening inner door.");

                    // attempt to open door
                    if (insideDoor != null)
                    {
                        insideDoor.OpenDoor();
                    }
                    else
                    {
                        // TODO: display a noisy error on LCD's
                        Echo("Fault at inner door. Perform maintenance check.");

                        // reset stage
                        airlockStage = 0;
                    }
                }
                else
                {
                    // TODO: display this on LCD's
                    Echo($"Airlock Pressurizing. Please Wait.");
                }
            }
        }

        private void HandleAirlockStageThree()
        {
            if (GoingIn())
            {
                if (airlockOxyLvl == "1.00")
                {
                    Echo("Airlock pressurized. Openining inner door.");

                    // attempt to open door
                    if (insideDoor != null)
                    {
                        insideDoor.OpenDoor();

                        airlockStage = 0;
                        habVents[0].CustomData = "";
                    }
                    else
                    {
                        // TODO: display a noisy error on LCD's
                        Echo("Fault at inner door. Perform maintenance check.");

                        // reset stage
                        airlockStage = 0;
                        habVents[0].CustomData = "";
                    }
                }
                else
                {
                    // TODO: display this on LCD's
                    Echo($"Airlock pressurizing. Please Wait.");
                }
            }
            else
            {
                if (airlockOxyLvl == "0.00")
                {
                    Echo("Airlock depressurized. Opening outter door.");

                    // attempt to open door
                    if (outsideDoor != null)
                    {
                        outsideDoor.OpenDoor();
                        airlockStage = 0;
                    }
                    else
                    {
                        // TODO: display a noisy error on LCD's
                        Echo("Fault at outter door.");

                        // reset stage
                        airlockStage = 0;
                        return;
                    }
                }
                else
                {
                    // TODO: display this on LCD's
                    Echo($"Airlock depressurizing. Please Wait.");
                }
            }
        }

        private void HandleAirlockReset()
        {
            if (airlockEntities.Count < 1)
            {
                foreach (var door in habDoors)
                {
                    // attempt to close doors
                    if (door != null)
                    {
                        door.CloseDoor();
                    }
                    else
                    {
                        // TODO: display a noisy error on LCD's
                        Echo($"Null door block at airlock doors index { habDoors.IndexOf(door) }. Perform maintenance check.");

                        return;
                    }
                }
                // depressurize room
                foreach (var vent in habVents)
                {
                    if (vent != null)
                    {
                        vent.Depressurize = true;
                    }
                    else
                    {
                        // TODO: display a noisy error on LCD's
                        Echo($"Null vent block at airlock vents index { habVents.IndexOf(vent) }. Perform maintenance check.");

                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Handles user input string passed in to main
        /// </summary>
        /// <param name="arg">argument passed in to main via user input</param>
        public void HandleUserInput(string arg)
        {
            if (arg != null)
            {
                switch (arg)
                {
                    // user going in from hangar
                    case "Hab In":
                        habVents[0].CustomData = "In";

                        if (airlockCanPressurize)
                        {
                            // airlock able to depressurize enum to stage 1
                            airlockStage++;

                            if (airlockOxyLvl == "0.00")
                            {
                                // attempt to open door
                                if (outsideDoor != null)
                                {
                                    // open door
                                    outsideDoor.OpenDoor();

                                    // stage 1 successful enum to stage 2
                                    airlockStage++;
                                }
                                else
                                {
                                    // TODO: display a noisy error on LCD's
                                    Echo("Fault at outer door. Perform maintenance check.");

                                    // reset stage
                                    airlockStage = 0;
                                    habVents[0].CustomData = "";
                                }
                            }
                            else
                            {
                                // attempt to depressurize room? Room is depressurized by default when not in use this may be unnecessary
                            }
                        }
                        else
                        {
                            Echo("Airlock unable to depressurize. Perform maintenance check.");
                            habVents[0].CustomData = "";
                        }

                        break;

                    // user going out from habitat facility
                    case "Hab Out":

                        if (airlockCanPressurize)
                        {
                            // airlock able to pressurize enum to stage 1
                            airlockStage++;

                            // attempt pressurize room
                            foreach (var vent in habVents)
                            {
                                if (vent != null)
                                {
                                    vent.Depressurize = false;
                                }
                                else
                                {
                                    // TODO: display a noisy error on LCD's
                                    Echo($"Null door block at airlock vents index { habVents.IndexOf(vent) }. Perform maintenance check.");

                                    // reset stage
                                    airlockStage = 0;
                                    break;
                                }
                            }

                            Echo("Airlock pressurizing.");

                            // stage 1 successful enum to stage 2
                            airlockStage++;
                        }
                        else
                        {
                            Echo("Airlock unable to pressurize. Perform maintenance check.");
                            airlockStage = 0;
                            break;
                        }

                        break;

                    // user cycles airlock to indicate everyone is inside and ready
                    case "Airlock Ready":

                        if (GoingIn())
                        {
                            if (outsideDoor != null)
                            {
                                Echo("Cycling airlock.");

                                // close door
                                outsideDoor.CloseDoor();

                                // pressurize room
                                foreach (var vent in habVents)
                                {
                                    if (vent != null)
                                    {
                                        vent.Depressurize = false;
                                    }
                                    else
                                    {
                                        // TODO: display a noisy error on LCD's
                                        Echo($"Null vent block at airlock vents index { habVents.IndexOf(vent) }. Perform maintenance check.");

                                        // reset stage
                                        airlockStage = 0;
                                        break;
                                    }

                                    // stage 2 successful enum to stage 3
                                    airlockStage++;
                                }
                            }
                            else
                            {
                                Echo($"Fault at outter door. Perform maintenance check.");
                                airlockStage = 0;
                                break;
                            }
                        }
                        else
                        {
                            // attempt to seal airlock - need to account for user going in from hangar, use a bool check (ie: goingIn, goingOut).
                            if (insideDoor != null)
                            {
                                Echo("Cycling airlock.");

                                // close door
                                insideDoor.CloseDoor();

                                // depressurize room
                                foreach (var vent in habVents)
                                {
                                    if (vent != null)
                                    {
                                        vent.Depressurize = true;
                                    }
                                    else
                                    {
                                        // TODO: display a noisy error on LCD's
                                        Echo($"Null vent block at airlock vents index { habVents.IndexOf(vent) }. Perform maintenance check.");

                                        // reset stage
                                        airlockStage = 0;
                                        break;
                                    }

                                    // stage 2 successful enum to stage 3
                                    airlockStage++;
                                }
                            }
                            else
                            {
                                Echo($"Fault at inner door. Perform maintenance check.");
                                airlockStage = 0;
                                break;
                            }
                        }

                        break;
                }
            }
        }

        //**HELPER METHODS**// 
        /// <summary>
        /// Checks airlock airvent custom data to see if user is going in or out of habitat area
        /// </summary>
        /// <returns>true if going in false if going out</returns>
        public bool GoingIn()
        {
            if (habVents[0].CustomData == "In")
            {
                return true;
            }
            return false;
        }
    }
}
