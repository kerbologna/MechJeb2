﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebCore : PartModule, IComparable<MechJebCore>
    {
        private const int windowIDbase = 60606;

        private List<ComputerModule> computerModules = new List<ComputerModule>();
        private List<DisplayModule> displayModules = new List<DisplayModule>();
        private bool modulesUpdated = false;

        public MechJebModuleAttitudeController attitude;
        public MechJebModuleStagingController staging;
        public MechJebModuleThrustController thrust;
        public MechJebModuleWarpController warp;

        public VesselState vesselState = new VesselState();

        private Vessel controlledVessel; //keep track of which vessel we've added our onFlyByWire callback to 

        DirectionTarget testTarget;
        IAscentPath testAscentPath = new DefaultAscentPath();

        //Returns whether the vessel we've registered OnFlyByWire with is the correct one. 
        //If it isn't the correct one, fixes it before returning false
        bool CheckControlledVessel()
        {
            if (controlledVessel == part.vessel) return true;

            //else we have an onFlyByWire callback registered with the wrong vessel:
            //handle vessel changes due to docking/undocking
            if (controlledVessel != null) controlledVessel.OnFlyByWire -= onFlyByWire;
            part.vessel.OnFlyByWire += onFlyByWire;
            controlledVessel = part.vessel;
            return false;
        }

        public int GetImportance()
        {
            if (part.State == PartStates.DEAD)
            {
                return 0;
            }
            else
            {
                return GetInstanceID();
            }
        }

        public int CompareTo(MechJebCore other)
        {
            if (other == null) return 1;
            return GetImportance().CompareTo(other.GetImportance());
        }

        public T GetComputerModule<T>() where T : ComputerModule
        {
            return (T)computerModules.First(a => a.GetType() == typeof(T));
        }

        public T GetDisplayModule<T>() where T : DisplayModule
        {
            return (T)displayModules.First(a => a.GetType() == typeof(T));
        }

        public ComputerModule GetComputerModule(string type)
        {
            return computerModules.First(a => a.GetType().Name.ToLowerInvariant() == type.ToLowerInvariant());
        }

        public DisplayModule GetDisplayModule(string type)
        {
            return displayModules.First(a => a.GetType().Name.ToLowerInvariant() == type.ToLowerInvariant());
        }

        public void AddComputerModule(ComputerModule module)
        {
            computerModules.Add(module);
            modulesUpdated = true;
        }

        public void AddDisplayModule(DisplayModule module)
        {
            displayModules.Add(module);
            modulesUpdated = true;
        }

        public override void OnStart(PartModule.StartState state)
        {
            AddComputerModule(attitude = new MechJebModuleAttitudeController(this));
            AddComputerModule(thrust = new MechJebModuleThrustController(this));
            AddComputerModule(staging = new MechJebModuleStagingController(this));
            AddComputerModule(warp = new MechJebModuleWarpController(this));

            AddComputerModule(new MechJebModuleAscentComputer(this));
            AddComputerModule(new MechJebModuleAscentGuidance(this));

            foreach (ComputerModule module in computerModules)
            {
                module.OnStart(state);
            }

            attitude.enabled = true; //for testing

            displayModules.Add(new MechJebModuleManeuverPlanner(this));

            displayModules[0].enabled = true; //for testing maneuver planner

            part.vessel.OnFlyByWire += drive;
            controlledVessel = part.vessel;
        }

        public override void OnActive()
        {
            foreach (ComputerModule module in computerModules)
            {
                module.OnActive();
            }
        }
        
        public override void OnInactive()
        {
            foreach (ComputerModule module in computerModules)
            {
                module.OnInactive();
            }
        }

        public override void OnAwake()
        {
            foreach (ComputerModule module in computerModules)
            {
                module.OnAwake();
            }
        }

        public void FixedUpdate()
        {
            CheckControlledVessel(); //make sure our onFlyByWire callback is registered with the right vessel

            if (this != vessel.GetMasterMechJeb())
            {
                return;
            }

            vesselState.Update(part.vessel);

            if (modulesUpdated)
            {
                computerModules.Sort();
                modulesUpdated = false;
            }

            foreach (ComputerModule module in computerModules)
            {
                module.OnFixedUpdate();
            }

            if (Input.GetKeyDown(KeyCode.Alpha9))
            {
/*                print("fwd: " + testTarget.GetFwdVector());
                print("name: " + testTarget.GetName());
                //print("obtvel: " + testTarget.GetObtVelocity());
                print("obt: " + testTarget.GetOrbit());
                print("obtdriver: " + testTarget.GetOrbitDriver());
                print("srfvel: " + testTarget.GetSrfVelocity());
                print("transform: " + testTarget.GetTransform());
                print("vessel: " + testTarget.GetVessel());*/
                testTarget = new DirectionTarget("Ascent Path Guidance");
                FlightGlobals.fetch.SetVesselTarget(testTarget);//FlightGlobals.fetch.vesselTargetDelta = FlightGlobals.fetch.vesselTargetDelta = (part.vessel.transform.position + 200 * part.vessel.transform.up);
            }

            if (testTarget != null)
            {
            }

        }

        public void Update()
        {
            if (this != vessel.GetMasterMechJeb())
            {
                return;
            }

            if (modulesUpdated)
            {
                computerModules.Sort();
                modulesUpdated = false;
            }

            if (Input.GetKey(KeyCode.Y))
            {
                print("prograde");
                attitude.attitudeTo(Vector3.forward, AttitudeReference.ORBIT, null);
            }
            if (Input.GetKey(KeyCode.U))
            {
                print("rad+");
                attitude.attitudeTo(Vector3.up, AttitudeReference.ORBIT, null);
            }
            if (Input.GetKey(KeyCode.B)) 
            {
                print("nml+");
                attitude.attitudeTo(Vector3.left, AttitudeReference.ORBIT, null);
            }


            foreach (ComputerModule module in computerModules)
            {
                module.OnUpdate();
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            print("MechJebCore.OnLoad");
            base.OnLoad(node); //is this necessary?

            ConfigNode type = new ConfigNode(KSP.IO.File.Exists<MechJebCore>("mechjeb_settings.cfg", vessel) ? KSP.IO.File.ReadAllText<MechJebCore>("mechjeb_settings.cfg", vessel) : "");
            ConfigNode global = new ConfigNode(KSP.IO.File.Exists<MechJebCore>("mechjeb_settings.cfg") ? KSP.IO.File.ReadAllText<MechJebCore>("mechjeb_settings.cfg") : "");

            foreach (ComputerModule module in computerModules)
            {
                module.OnLoad(node, type, global);
            }
            foreach (DisplayModule module in displayModules)
            {
                module.OnLoad(node, type, global);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            print("MechJebCore.OnSave");
            base.OnSave(node); //is this necessary?

            ConfigNode type = new ConfigNode(KSP.IO.File.Exists<MechJebCore>("mechjeb_settings.cfg", vessel) ? KSP.IO.File.ReadAllText<MechJebCore>("mechjeb_settings.cfg", vessel) : "");
            ConfigNode global = new ConfigNode(KSP.IO.File.Exists<MechJebCore>("mechjeb_settings.cfg") ? KSP.IO.File.ReadAllText<MechJebCore>("mechjeb_settings.cfg") : "");

            foreach (ComputerModule module in computerModules)
            {
                module.OnSave(node, type, global);
            }
            foreach (DisplayModule module in displayModules)
            {
                module.OnSave(node, type, global);
            }

            KSP.IO.File.WriteAllText<MechJebCore>(type.ToString(), "mechjeb_settings.cfg", vessel);
            KSP.IO.File.WriteAllText<MechJebCore>(global.ToString(), "mechjeb_settings.cfg");
        }

        public void OnDestroy()
        {
            print("MechJebCore.OnDestroy");
            foreach (ComputerModule module in computerModules)
            {
                module.OnDestroy();
            }

            vessel.OnFlyByWire -= onFlyByWire;
            controlledVessel = null;
        }

        private void onFlyByWire(FlightCtrlState s)
        {
            if (!CheckControlledVessel() || this != vessel.GetMasterMechJeb())
            {
                return;
            }

            drive(s);

            if (vessel == FlightGlobals.ActiveVessel)
            {
                FlightInputHandler.state.mainThrottle = s.mainThrottle; //so that the on-screen throttle gauge reflects the autopilot throttle
            }
        }

        private void drive(FlightCtrlState s)
        {
            //do we need to do something to prevent conflicts here?
            foreach (ComputerModule module in computerModules)
            {
                if (module.enabled) module.Drive(s);
            }
        }

        private void OnGUI()
        {
            if ((FlightGlobals.ready) && (vessel == FlightGlobals.ActiveVessel) && (part.State != PartStates.DEAD) && (this == vessel.GetMasterMechJeb()))
            {
                int wid = 0;
                foreach (DisplayModule module in displayModules)
                {
                    if (module.enabled) module.DrawGUI(windowIDbase + wid);
                    wid++;
                }
            }
        }

        // VAB/SPH description
        public override string GetInfo()
        {
            return "Attitude control by MechJeb™";
        }
    }
}