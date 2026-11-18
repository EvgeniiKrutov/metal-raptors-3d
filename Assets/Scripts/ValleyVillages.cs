using UnityEngine;

namespace MetalRaptors
{
    public struct VillageSite
    {
        public VosgesLayout.VillageSpot spot;
        public float left, right;
        public Vector3[] corners;
        public float CentreX => (left + right) * 0.5f;

        public float CentreZ
        {
            get
            {
                if (corners == null || corners.Length == 0) return spot.origin.z;

                float sum = 0f;
                foreach (Vector3 corner in corners) sum += corner.z;
                return sum / corners.Length;
            }
        }

        public bool Seen(float camX, float camZ, float distance, float halfWidth)
        {
            if (corners == null) return false;

            float low = float.MaxValue, high = float.MinValue;
            foreach (Vector3 corner in corners)
            {
                float depth = Mathf.Max(1f, corner.z - camZ);
                float offset = (corner.x - camX) * distance / depth;
                low = Mathf.Min(low, offset);
                high = Mathf.Max(high, offset);
            }
            return low <= halfWidth && high >= -halfWidth;
        }

        public float EntryCamX(float camZ, float distance, float halfWidth)
        {
            if (corners == null) return left - halfWidth;

            float entry = float.MaxValue;
            foreach (Vector3 corner in corners)
                entry = Mathf.Min(entry, corner.x - halfWidth * Mathf.Max(1f, corner.z - camZ) / distance);
            return entry;
        }
    }

    public static class ValleyVillages
    {
        const float U = PlaneModelConfig.UnitsPerMeter;
        const int SearchCells = 6;

        public static Vector3 World(VosgesLayout.VillageSpot spot, Vector3 local) =>
            spot.origin + spot.rotation * (local * U);

        public static bool Site(VosgesLayout layout, int cell, out VillageSite site)
        {
            site = default;
            if (layout == null || !layout.Village(cell, out VosgesLayout.VillageSpot spot)) return false;

            Bounds box = VosgesVillage.Footprint(spot.model);
            float left = float.MaxValue, right = float.MinValue;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? box.min.x : box.max.x,
                    (i & 2) == 0 ? box.min.y : box.max.y,
                    (i & 4) == 0 ? box.min.z : box.max.z);
                corners[i] = World(spot, corner);
                left = Mathf.Min(left, corners[i].x);
                right = Mathf.Max(right, corners[i].x);
            }

            site = new VillageSite { spot = spot, left = left, right = right, corners = corners };
            return true;
        }

        public static bool Entered(VosgesLayout layout, float camX, float camZ, float distance,
            float halfWidth, out VillageSite site)
        {
            site = default;
            if (layout == null) return false;

            bool found = false;
            int middle = Mathf.FloorToInt(camX / layout.VillageCell);
            for (int cell = middle - SearchCells; cell <= middle + SearchCells; cell++)
            {
                if (!Site(layout, cell, out VillageSite candidate)) continue;
                if (!candidate.Seen(camX, camZ, distance, halfWidth)) continue;
                if (found && candidate.CentreX <= site.CentreX) continue;

                site = candidate;
                found = true;
            }
            return found;
        }

        public static bool Ahead(VosgesLayout layout, float fromX, out VillageSite site)
        {
            site = default;
            if (layout == null) return false;

            bool found = false;
            int first = Mathf.FloorToInt(fromX / layout.VillageCell) - 1;
            for (int cell = first; cell <= first + SearchCells * 2; cell++)
            {
                if (!Site(layout, cell, out VillageSite candidate) || candidate.left <= fromX) continue;
                if (found && candidate.left >= site.left) continue;

                site = candidate;
                found = true;
            }
            return found;
        }
    }
}
