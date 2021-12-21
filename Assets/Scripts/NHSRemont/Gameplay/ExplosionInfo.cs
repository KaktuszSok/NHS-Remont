using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace NHSRemont.Gameplay
{
    public readonly struct ExplosionInfo
    {
        /// <summary>
        /// How much energy does 1kg of tnt have?
        /// </summary>
        public const float joulesPerKgTnt = 4.184e6f;
        /// <summary>
        /// What percentage of the explosion's energy gets translated into kinetic energy applied to rigidbodies
        /// </summary>
        private const float energyConversionEfficiency = 0.05f;
        /// <summary>
        /// When calculating an explosion from tnt mass, this value determines where the blast radius ends.
        /// </summary>
        public const float minimumJoulesPerSquareMetre = 9f;
        /// <summary>
        /// Minimum effective value for distance when doing calculations. Any objects closer than this will be treated as if the distance to them was equal to this value.
        /// </summary>
        private const float minDistance = 1.25f;

        /// <summary>
        /// The position of the explosion
        /// </summary>
        public readonly Vector3 position;
        /// <summary>
        /// The radius of the explosion's effect
        /// </summary>
        public readonly float blastRadius;
        /// <summary>
        /// The energy released by the explosion, in joules
        /// </summary>
        public readonly double power;
        /// <summary>
        /// The squared distance is further raised to this power when calculating the energy at a point
        /// </summary>
        public readonly float energyFalloffExponent;
        /// <summary>
        /// The upwards modifier of the explosion
        /// </summary>
        public readonly float upwardsModifier;

        public float power_tnt => (float)(power / joulesPerKgTnt);

        // /// <summary>
        // /// Constructor which allows you to set the blast radius and power (in joules) directly
        // /// </summary>
        // /// <param name="position">The position of the explosion</param>
        // /// <param name="blastRadius">How far away does this explosion affect objects</param>
        // /// <param name="power">The energy released by this explosion, in joules.</param>
        // /// <param name="upwardsModifier">The explosion will push objects in a direction as if it originated this many metres lower than the actual position.</param>
        // /// <param name="energyFalloffExponent">Affects how quickly the explosion's impact decreases over distance. Effective falloff is distance^(2*this).</param>
        // public ExplosionInfo(Vector3 position, float blastRadius, float power, float upwardsModifier=0f, float energyFalloffExponent = 1.56f)
        // {
        //     this.position = position;
        //     this.blastRadius = blastRadius;
        //     this.power = power;
        //     this.upwardsModifier = upwardsModifier;
        //     this.energyFalloffExponent = 1.56f;
        // }

        /// <summary>
        /// Constructor which automatically determines blast radius and power given an equivalent mass of TNT
        /// </summary>
        /// <param name="position">The position of the explosion</param>
        /// <param name="tnt_kg">The yield of the explosion, in kilogrammes of TNT</param>
        /// <param name="upwardsModifier">The explosion will push objects in a direction as if it originated this many metres lower than the actual position.</param>
        /// <param name="energyFalloffExponent">Affects how quickly the explosion's impact decreases over distance. Effective falloff is distance^(2*this).</param>
        public ExplosionInfo(Vector3 position, float tnt_kg, float upwardsModifier = 0f, float energyFalloffExponent = 1.5f)
        {
            this.position = position;
            this.power = tnt_kg * joulesPerKgTnt;
            this.upwardsModifier = upwardsModifier;
            this.energyFalloffExponent = energyFalloffExponent;
            //minJoulesPerSqM (call it I) = E*((A/4pi*(r^2)^falloff))*efficiency
            //I = E*(1/4pi*(r^2)^falloff)*efficiency
            //I = E*efficiency/(4pi*(r^2)^falloff)
            //I*4pi*(r^2)^falloff = E*efficiency
            //(r^2)^falloff = E*efficiency/(I*4pi)
            //r = root(2*falloff, E*efficiency/(I*4pi))
            //r = pow(E*efficiency/(I*4pi), 1/(2*falloff))
            double equationBase = power*energyConversionEfficiency / (minimumJoulesPerSquareMetre * 4*Mathf.PI);
            double equationPower = 1d / (2 * energyFalloffExponent);
            this.blastRadius = (float)Math.Pow(equationBase, equationPower);

            Debug.Log("radius=" + blastRadius);
        }

        //using a tuple so that we don't cause accidental overload conflicts
        public ExplosionInfo((Vector3 position, float blastRadius, double power, float energyFalloffExponent, float upwardsModifier) directParameters)
        {
            this.position = directParameters.position;
            this.blastRadius = directParameters.blastRadius;
            this.power = directParameters.power;
            this.energyFalloffExponent = directParameters.energyFalloffExponent;
            this.upwardsModifier = directParameters.upwardsModifier;
        }

        public void ApplyToRigidbody(Rigidbody rb)
        {
            Vector3 contactPoint = rb.ClosestPointOnBounds(position);
            Vector3 adjustedPosition = new Vector3(position.x, position.y - upwardsModifier, position.z); //position adjusting for the upwards modifier
            Vector3 direction = (contactPoint - adjustedPosition).normalized;
            float sqDist = (contactPoint - position).sqrMagnitude;
            if(sqDist > blastRadius*blastRadius) return;
            sqDist = Mathf.Max(sqDist, minDistance*minDistance);

            float area = PhysicsManager.instance.EstimateCrossSection(rb);
            float impulse = CalculateImpulse(sqDist, area, rb.mass);
            rb.AddForceAtPosition(impulse*direction, contactPoint, ForceMode.Impulse);
        }

        public void ApplyToRigidbodies(IEnumerable<Rigidbody> rbs)
        {
            foreach (Rigidbody rb in rbs)
            {
                ApplyToRigidbody(rb);
            }
        }

        /// <returns>The impulse this explosion would apply to this rigidbody, the world point at which it would be applied, and the square distance from the explosion's centre to said point</returns>
        public (Vector3 impulse, Vector3 point, float sqrDist) CalculateImpulseAndPoint(Rigidbody rb)
        {
            Vector3 contactPoint = rb.ClosestPointOnBounds(position);
            Vector3 adjustedPosition = new Vector3(position.x, position.y - upwardsModifier, position.z); //position adjusting for the upwards modifier
            Vector3 direction = (contactPoint - adjustedPosition).normalized;
            float sqDist = (contactPoint - position).sqrMagnitude;
            if(sqDist > blastRadius*blastRadius) return (Vector3.zero, contactPoint, sqDist);
            sqDist = Mathf.Max(sqDist, minDistance*minDistance);

            float area = PhysicsManager.instance.EstimateCrossSection(rb);
            float impulse = CalculateImpulse(sqDist, area, rb.mass);
            return (impulse * direction, contactPoint, sqDist);
        }


        // /// <returns>The impulse this explosion would apply to this body, and the world point at which it would be applied</returns>
        // public (Vector3, Vector3) CalculateImpulseAndPoint(Transform body, Bounds bodyBounds, float mass)
        // {
        //     Vector3 contactPoint = bodyBounds.ClosestPoint(position);
        //     float sqDist = (contactPoint - position).sqrMagnitude;
        //     if(sqDist > blastRadius*blastRadius) return (Vector3.zero, body.position);
        //     
        //     Vector3 adjustedPosition = new Vector3(position.x, position.y - upwardsModifier, position.z); //position adjusting for the upwards modifier
        //     Vector3 direction = (contactPoint - adjustedPosition).normalized;
        //     sqDist = Mathf.Max(sqDist, minDistance*minDistance);
        //     
        //     float area = PhysicsManager.instance.EstimateCrossSection(body, bodyBounds);
        //     return (CalculateImpulse(sqDist, area, mass) * direction, contactPoint);
        // }
        
        /// <returns>The impulse this explosion would apply to this body, the world point at which it would be applied, and the square distance from the explosion's centre to said point</returns>
        public (Vector3 impulse, Vector3 point, float sqrDist) CalculateImpulseAndPoint(Transform body, Collider bodyCollider, float mass)
        {
            bool disable = false;
            if (!bodyCollider.enabled)
            {
                bodyCollider.enabled = true;
                //Physics.SyncTransforms();
                disable = true;
            }
            Vector3 contactPoint = bodyCollider.ClosestPoint(position);
            if (disable)
            {
                bodyCollider.enabled = false;
                //Physics.SyncTransforms();
            }
            float sqDist = (contactPoint - position).sqrMagnitude;
            if(sqDist > blastRadius*blastRadius) return (Vector3.zero, body.position, sqDist);
            
            Vector3 adjustedPosition = new Vector3(position.x, position.y - upwardsModifier, position.z); //position adjusting for the upwards modifier
            Vector3 direction = (contactPoint - adjustedPosition).normalized;
            sqDist = Mathf.Max(sqDist, minDistance*minDistance);
            
            float area = PhysicsManager.instance.EstimateCrossSection(bodyCollider);
            return (CalculateImpulse(sqDist, area, mass) * direction, contactPoint, sqDist);
        }
        
        /// <returns>Magnitude of the impulse</returns>
        public float CalculateImpulse(float sqrDistance, float surfaceArea, float rbMass)
        {
            sqrDistance = Mathf.Max(sqrDistance, minDistance*minDistance);
            sqrDistance = Mathf.Pow(sqrDistance, energyFalloffExponent);
            surfaceArea = LimitSurfaceArea(surfaceArea, sqrDistance);
            double energy = GetConeEnergy(sqrDistance, surfaceArea)*energyConversionEfficiency;
            return (float)Math.Sqrt(2 * energy * rbMass);
        }

        /// <summary>
        /// Gets the overpressure caused by this explosion at some distance
        /// </summary>
        /// <param name="sqrDistance">Square distance to the explosion's centre</param>
        /// <returns>The overpressure caused, in kPa.</returns>
        public float GetOverpressureAt(float sqrDistance)
        {
            sqrDistance = Mathf.Max(sqrDistance, minDistance*minDistance);
            return (float) (power*energyConversionEfficiency / GetTotalVolumeAt(Mathf.Sqrt(sqrDistance)) / 1000d);
        }

        /// <summary>
        /// Gets the amount of this explosion's energy caught by a surface at some distance
        /// </summary>
        /// <param name="sqrDistance">Square distance to the explosion's centre</param>
        /// <param name="surfaceArea">The exposed area of the surface catching this energy</param>
        /// <returns>The caught energy, in Joules</returns>
        public double GetEnergyCaughtBySurfaceAt(float sqrDistance, float surfaceArea)
        {
            sqrDistance = Mathf.Max(sqrDistance, minDistance*minDistance);
            return GetConeEnergy(sqrDistance, LimitSurfaceArea(surfaceArea, sqrDistance))*energyConversionEfficiency;
        }

        /// <returns>True if the point is within the blast radius</returns>
        public bool IsPointWithinBlastRadius(Vector3 point)
        {
            return (position - point).sqrMagnitude <= blastRadius * blastRadius;
        }

        /// <returns>True if the bounding box is (partially or fully) within the blast radius. Also returns the closest point to the explosion on (or inside) the given bounds.</returns>
        public (bool withinBlastRadius, Vector3 closestPoint) IsBoundingBoxWithinBlastRadius(Bounds bounds)
        {
            Vector3 contactPoint = bounds.ClosestPoint(position);
            float sqDist = (contactPoint - position).sqrMagnitude;
            return (sqDist <= blastRadius*blastRadius, contactPoint);
        }

        /// <summary>
        /// Calculates the energy contained in a conical sub-volume of this explosion.
        /// </summary>
        /// <param name="sqrDistance">Square distance to the explosion's centre</param>
        /// <param name="limitedArea">The limited surface area catching this energy</param>
        /// <returns>The energy in the cone</returns>
        private double GetConeEnergy(float sqrDistance, float limitedArea)
        {
            float solidAngleFraction = (limitedArea / (4 * Mathf.PI * sqrDistance));
            if (float.IsNaN(solidAngleFraction) || float.IsInfinity(solidAngleFraction))
                return power * 0.5d;
            return power * solidAngleFraction;
        }

        /// <summary>
        /// Limits the given area to not exceed half the surface area of the explosion at a certain distance
        /// </summary>
        private float LimitSurfaceArea(float surfaceArea, float sqrDistance)
        {
            return Mathf.Min(surfaceArea, 2 * Mathf.PI * sqrDistance);
        }

        /// <summary>
        /// Gets the total volume of the explosion at some distance
        /// </summary>
        private float GetTotalVolumeAt(float distance)
        {
            return (3/4f) * Mathf.PI * distance*distance*distance;
        }

        public short Serialise(StreamBuffer outStream)
        {
            byte[] floats7 = new byte[sizeof(float)*6 + sizeof(double)];

            int index = 0;
            Protocol.Serialize(position.x, floats7, ref index);
            Protocol.Serialize(position.y, floats7, ref index);
            Protocol.Serialize(position.z, floats7, ref index);
            Protocol.Serialize(blastRadius, floats7, ref index);
            Protocol.Serialize(power_tnt, floats7, ref index);
            Protocol.Serialize(energyFalloffExponent, floats7, ref index);
            Protocol.Serialize(upwardsModifier, floats7, ref index);
            
            outStream.Write(floats7, 0, sizeof(float)*7);

            return sizeof(float)*7;
        }

        public static ExplosionInfo Deserialise(StreamBuffer inStream)
        {
            byte[] bytes4 = new byte[sizeof(float)];
            
            Vector3 pos;
            inStream.Read(bytes4, 0, sizeof(float));
            int offset = 0;
            Protocol.Deserialize(out pos.x, bytes4, ref offset);
            
            inStream.Read(bytes4, 0, sizeof(float));
            offset = 0;
            Protocol.Deserialize(out pos.y, bytes4, ref offset);
            
            inStream.Read(bytes4, 0, sizeof(float));
            offset = 0;
            Protocol.Deserialize(out pos.z, bytes4, ref offset);

            inStream.Read(bytes4, 0, sizeof(float));
            offset = 0;
            Protocol.Deserialize(out float blastRadius, bytes4, ref offset);
            
            inStream.Read(bytes4, 0, sizeof(float));
            offset = 0;
            Protocol.Deserialize(out float power, bytes4, ref offset);

            inStream.Read(bytes4, 0, sizeof(float));
            offset = 0;
            Protocol.Deserialize(out float falloff, bytes4, ref offset);
            
            inStream.Read(bytes4, 0, sizeof(float));
            offset = 0;
            Protocol.Deserialize(out float upMod, bytes4, ref offset);

            return new ExplosionInfo((pos, blastRadius, (double)power*joulesPerKgTnt, falloff, upMod));
        }

        public override string ToString()
        {
            return "(Explosion | pos=" + position + " | pow=" + power + " | radius=" + blastRadius + ")";
        }
    }
}