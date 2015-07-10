/*
Trajectories
Copyright 2014, Youen Toupin

This file is part of Trajectories, under MIT license.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;

namespace Trajectories
{
    // this class abstracts the game aerodynamic computations to provide an unified interface wether the stock drag is used, or a supported mod is installed
    public class FARAerodynamicModel : VesselAerodynamicModel
    {
        public override string Name { get { return "FAR"; } }

        public static int Priority { get { return 100; } }

        private static Type FARAPIType;
        private static MethodInfo FARAPI_CalculateVesselAeroForces;
		private static MethodInfo FARAeroUtil_GetCurrentDensity;

        public new static int Initialize()
        {
            if (FARAPIType != null)
                return Priority;

            Debug.Log("Trajectories: Initializing FAR aerodynamic model...");

            bool farInstalled = false;

            foreach (var loadedAssembly in AssemblyLoader.loadedAssemblies)
            {
                try
                {
                    switch (loadedAssembly.name)
                    {
                        case "FerramAerospaceResearch":
                            string namespaceName = "FerramAerospaceResearch";
                            FARAPIType = loadedAssembly.assembly.GetType(namespaceName + ".FARAPI");

                            // public static void FerramAerospaceResearch.FARAPI.CalculateVesselAeroForces(Vessel vessel, out Vector3 aeroForce, out Vector3 aeroTorque, Vector3 velocityWorldVector, double altitude)
                            FARAPI_CalculateVesselAeroForces = FARAPIType.GetMethodEx("CalculateVesselAeroForces", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(Vessel), typeof(Vector3).MakeByRefType(), typeof(Vector3).MakeByRefType(), typeof(Vector3), typeof(double) });

                            // public static double FerramAerospaceResearch.FARAeroUtil.GetCurrentDensity(CelestialBody body, double altitude, bool densitySmoothingAtOcean = true)
                            FARAeroUtil_GetCurrentDensity = loadedAssembly.assembly.GetType(namespaceName + ".FARAeroUtil").GetMethodEx("GetCurrentDensity", new Type[] { typeof(CelestialBody), typeof(double), typeof(bool) });

                            farInstalled = true;
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("Trajectories: failed to interface with assembly " + loadedAssembly.name);
                    Debug.Log(e.ToString());
                }
            }

            return farInstalled ? Priority : -1;
        }

        public FARAerodynamicModel(Vessel vessel)
            : base(vessel)
        {
            if (FARAPIType == null)
                throw new Exception("Trajectories/FAR is not initialized");
        }
        
        public override Vector3d ComputeForces(CelestialBody body, double altitude, double airVelocity, double angleOfAttack)
        {
            Transform vesselTransform = Vessel_.ReferenceTransform;

            Vector3d vesselBackward = (Vector3d)(-vesselTransform.up.normalized);
            Vector3d vesselForward = -vesselBackward;
            Vector3d vesselUp = (Vector3d)(-vesselTransform.forward.normalized);
            Vector3d vesselRight = Vector3d.Cross(vesselUp, vesselBackward).normalized;

            Vector3d airVelocityForFixedAoA = (vesselForward * Math.Cos(-angleOfAttack) + vesselUp * Math.Sin(-angleOfAttack)) * airVelocity;

            Vector3 worldAirVel = new Vector3((float)airVelocityForFixedAoA.x, (float)airVelocityForFixedAoA.y, (float)airVelocityForFixedAoA.z);
            var parameters = new object[] { Vessel_, new Vector3(), new Vector3(), worldAirVel, altitude };
            FARAPI_CalculateVesselAeroForces.Invoke(null, parameters);
            Vector3d totalForce = (Vector3)parameters[1];

            if (Double.IsNaN(totalForce.x) || Double.IsNaN(totalForce.y) || Double.IsNaN(totalForce.z))
            {
                Debug.Log("Trajectories: WARNING: FAR/NEAR totalForce is NAN (altitude=" + altitude + ", airVelocity=" + airVelocity + ", angleOfAttack=" + angleOfAttack);
                return new Vector3d(0, 0, 0); // Don't send NaN into the simulation as it would cause bad things (infinite loops, crash, etc.). I think this case only happens at the atmosphere edge, so the total force should be 0 anyway.
            }

            Vector3d localForce = new Vector3d(Vector3d.Dot(vesselRight, totalForce), Vector3d.Dot(vesselUp, totalForce), Vector3d.Dot(vesselBackward, totalForce));

            return localForce;
        }
    }
}
