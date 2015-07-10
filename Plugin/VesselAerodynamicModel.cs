/*
Trajectories
Copyright 2014, Youen Toupin

This file is part of Trajectories, under MIT license.
*/

#define USE_CACHE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;

namespace Trajectories
{
    /// <summary>
    /// Abstracts the game aerodynamic computations to provide an unified interface, wether the stock model is used, or a supported mod is installed
    /// </summary>
    public abstract class VesselAerodynamicModel
    {
        private double Mass_;
        public double Mass { get { return Mass_; } }

        public abstract string Name { get; }

        protected Vessel Vessel_;

        private static Type ModelToUse;

        /// <summary>
        /// Enumerates all available aerodynamic models, tries to initialize them, and chooses the one that can be used and has the highest priority.
        /// </summary>
        public static void Initialize()
        {
            ModelToUse = null;
            int bestPriority = -1;
            var candidateModels = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(VesselAerodynamicModel)) && !t.IsAbstract).ToArray();
            foreach(var model in candidateModels)
            {
                int result = (int)model.InvokeMember("Initialize", BindingFlags.Public | BindingFlags.Static, null, null, new object[0]);
                if(result >= 0)
                {
                    if (ModelToUse == null || result >= bestPriority)
                    {
                        if (result == bestPriority)
                            throw new Exception("Multiple aerodynamic models with the same priority");

                        ModelToUse = model;
                        bestPriority = result;
                    }
                }
            }

            if(ModelToUse == null)
                throw new Exception("No aerodynamic model can be used");
        }

        public static VesselAerodynamicModel CreateModel(Vessel vessel)
        {
            return (VesselAerodynamicModel)Activator.CreateInstance(ModelToUse, new object[] { vessel });
        }
        
        public VesselAerodynamicModel(Vessel vessel)
        {
            Vessel_ = vessel;
            UpdateVesselInfo();
        }

        private void UpdateVesselInfo()
        {
            Mass_ = 0.0;
            foreach (var part in Vessel_.Parts)
            {
                if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
                    continue;

                float partMass = part.mass + part.GetResourceMass();
                Mass_ += partMass;
            }
        }

        public virtual void Update()
        {
            UpdateVesselInfo();
        }

        /// <summary>
        /// Computes the aerodynamic forces, local to the vessel
        /// </summary>
        /// <param name="body">The body whose atmosphere the vessel is travelling</param>
        /// <param name="altitude">The altitude, in meters, above sea level</param>
        /// <param name="airVelocity">The velocity of air, relatively to the vessel, in meters per second</param>
        /// <param name="angleOfAttack">The angle at which the vessel travels in air, in radians. 0 means the vessel travels forward (up for a rocket), positive values mean the air comes from below (for example when a plane is loosing altitude while staying horizontal)</param>
        /// <returns></returns>
        public abstract Vector3d ComputeForces(CelestialBody body, double altitude, double airVelocity, double angleOfAttack);
    }
}
