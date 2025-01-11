using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.PlugAndPlayV2.Helpers
{
    public static class PlanetHelper
    {
        public static bool IsSurfaceFlat(MyPlanet planet, Vector3D surfacePoint, double radius, double flatnessThreshold, double heightThreshold)
        {
            Vector3D planetCenter = planet.PositionComp.WorldAABB.Center;

            // Directions to sample around the center point
            Vector3D[] sampleOffsets = new Vector3D[]
            {
                new Vector3D(1, 0, 0),  // Forward
                new Vector3D(-1, 0, 0), // Backward
                new Vector3D(0, 1, 0),  // Right
                new Vector3D(0, -1, 0), // Left
            };

            // Reference height and up vector
            double referenceHeight = Vector3D.Distance(surfacePoint, planetCenter);
            Vector3D referenceUp = Vector3D.Normalize(surfacePoint - planetCenter);

            foreach (var offset in sampleOffsets)
            {
                // Get the neighboring point
                Vector3D neighborPoint = surfacePoint + offset * radius;

                // Find the actual surface point for the neighbor
                Vector3D sampledPoint = planet.GetClosestSurfacePointGlobal(neighborPoint);

                // Calculate height difference
                double sampledHeight = Vector3D.Distance(sampledPoint, planetCenter);
                double heightDifference = Math.Abs(sampledHeight - referenceHeight);
                if (heightDifference > heightThreshold)
                    return false;

                // Calculate normal vector at the neighbor point
                Vector3D sampledUp = Vector3D.Normalize(sampledPoint - planetCenter);

                // Compare the angle between normals
                double angleBetween = Vector3D.Dot(referenceUp, sampledUp);
                if (Math.Acos(angleBetween) > flatnessThreshold * (Math.PI / 180))
                    return false;
            }

            return true; // The area is flat enough
        }

        public static Tuple<Vector3D, Vector3D, Vector3D> GetSurfacePositionWithForward(MyPlanet planet, double radius = 10.0, double flatnessThreshold = 5.0, double heightThreshold = 2.0, int maxRetries = 10)
        {
            Vector3D planetCenter = planet.PositionComp.WorldAABB.Center;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                // Generate a random direction
                Vector3D randomDirection = Vector3D.Normalize(new Vector3D(
                    MyUtils.GetRandomDouble(-1, 1),
                    MyUtils.GetRandomDouble(-1, 1),
                    MyUtils.GetRandomDouble(-1, 1)));

                Vector3D directionFromCenter = Vector3D.Normalize(randomDirection);

                // Find a point on the planet's surface
                var surfacePoint = planet.GetClosestSurfacePointGlobal(planetCenter + (directionFromCenter * 100000));

                // Calculate the "up" vector (normal to the surface)
                Vector3D upVector = Vector3D.Normalize(surfacePoint - planetCenter);

                // Define a stable forward vector orthogonal to the up vector
                Vector3D arbitraryVector = Vector3D.Forward; // Default fallback
                if (Vector3D.IsZero(Vector3D.Cross(upVector, arbitraryVector)))
                {
                    arbitraryVector = Vector3D.Right; // Use a different vector if upVector is aligned with Forward
                }

                // Compute the forward vector
                Vector3D forwardVector = Vector3D.Normalize(Vector3D.Cross(arbitraryVector, upVector));

                // Ensure forward and up are orthogonal
                Vector3D rightVector = Vector3D.Cross(forwardVector, upVector);
                forwardVector = Vector3D.Cross(upVector, rightVector);

                // Check if the area is flat
                if (IsSurfaceFlat(planet, surfacePoint, radius, flatnessThreshold, heightThreshold))
                {
                    return Tuple.Create(surfacePoint, upVector, forwardVector);
                }
            }

            // Return null if no flat area is found after maxRetries
            return null;
        }
    }
}
