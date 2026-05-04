using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace LSOL.Systems
{
    public sealed class BarrierInteractionHandler
    {
        private const float BarrierInteractDistance = 8f;
        private const float BarrierOpenAngleDegrees = 82f;

        private readonly int[] _barrierModelHashes;
        private readonly HashSet<int> _animatedBarrierModelHashes;
        private readonly Dictionary<int, float> _barrierClosedHeadings;

        public BarrierInteractionHandler()
        {
            _barrierModelHashes = CreateBarrierModelHashes();
            _animatedBarrierModelHashes = new HashSet<int>(CreateAnimatedBarrierModelHashes());
            _barrierClosedHeadings = new Dictionary<int, float>();
        }

        public bool TryGetNearestBarrier(Vector3 playerPos, out Prop nearestBarrier)
        {
            nearestBarrier = null;
            if (_barrierModelHashes == null || _barrierModelHashes.Length == 0)
            {
                return false;
            }

            var bestDistanceSq = BarrierInteractDistance * BarrierInteractDistance;

            for (int i = 0; i < _barrierModelHashes.Length; i++)
            {
                var modelHash = _barrierModelHashes[i];
                var handle = Function.Call<int>(
                    Hash.GET_CLOSEST_OBJECT_OF_TYPE,
                    playerPos.X,
                    playerPos.Y,
                    playerPos.Z,
                    BarrierInteractDistance,
                    modelHash,
                    false,
                    false,
                    false);

                if (handle <= 0)
                {
                    continue;
                }

                var barrier = Entity.FromHandle(handle) as Prop;
                if (barrier == null || !barrier.Exists())
                {
                    continue;
                }

                var distanceSq = barrier.Position.DistanceToSquared(playerPos);
                if (distanceSq > bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                nearestBarrier = barrier;
            }

            return nearestBarrier != null;
        }

        public bool TryOpenNearbyBarrier(Ped player)
        {
            if (player == null || !player.Exists())
            {
                return false;
            }

            Prop nearestBarrier = null;
            if (!TryGetNearestBarrier(player.Position, out nearestBarrier))
            {
                return false;
            }

            if (_animatedBarrierModelHashes.Contains(nearestBarrier.Model.Hash))
            {
                return TryOpenBarrierWithNativeAnimation(nearestBarrier);
            }

            float closedHeading;
            if (!_barrierClosedHeadings.TryGetValue(nearestBarrier.Handle, out closedHeading))
            {
                closedHeading = nearestBarrier.Heading;
                _barrierClosedHeadings[nearestBarrier.Handle] = closedHeading;
            }

            var playerLocalOffset = nearestBarrier.GetPositionOffset(player.Position);
            var sideSign = playerLocalOffset.X >= 0f ? -1f : 1f;
            nearestBarrier.Heading = closedHeading + (BarrierOpenAngleDegrees * sideSign);
            return true;
        }

        public void ClearState()
        {
            _barrierClosedHeadings.Clear();
        }

        private bool TryOpenBarrierWithNativeAnimation(Prop barrier)
        {
            if (barrier == null || !barrier.Exists())
            {
                return false;
            }

            var modelHash = barrier.Model.Hash;
            if (!_animatedBarrierModelHashes.Contains(modelHash))
            {
                return false;
            }

            try
            {
                var pos = barrier.Position;
                int doorSystemHash;
                if (!TryGetDoorSystemHash(pos, modelHash, out doorSystemHash))
                {
                    doorSystemHash = BuildDoorSystemHash(barrier);
                    if (!Function.Call<bool>(Hash.IS_DOOR_REGISTERED_WITH_SYSTEM, doorSystemHash))
                    {
                        Function.Call(
                            Hash.ADD_DOOR_TO_SYSTEM,
                            doorSystemHash,
                            modelHash,
                            pos.X,
                            pos.Y,
                            pos.Z,
                            false,
                            false,
                            false);
                    }
                }

                Function.Call(Hash.DOOR_SYSTEM_SET_DOOR_STATE, doorSystemHash, 0, true, true);
                Function.Call(Hash.DOOR_SYSTEM_SET_HOLD_OPEN, doorSystemHash, true);
                Function.Call(Hash.DOOR_SYSTEM_SET_OPEN_RATIO, doorSystemHash, 1f, true, true);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryGetDoorSystemHash(Vector3 position, int modelHash, out int doorSystemHash)
        {
            doorSystemHash = 0;
            var outputHash = new OutputArgument();
            var found = Function.Call<bool>(
                Hash.DOOR_SYSTEM_FIND_EXISTING_DOOR,
                position.X,
                position.Y,
                position.Z,
                modelHash,
                outputHash);

            if (!found)
            {
                return false;
            }

            doorSystemHash = outputHash.GetResult<int>();
            return doorSystemHash != 0;
        }

        private static int BuildDoorSystemHash(Prop barrier)
        {
            if (barrier == null)
            {
                return 1;
            }

            var composed = unchecked((uint)(0x5A000000u ^ (uint)barrier.Handle ^ (uint)barrier.Model.Hash));
            if (composed == 0u)
            {
                composed = 1u;
            }

            return unchecked((int)composed);
        }

        private static int[] CreateBarrierModelHashes()
        {
            return new[]
                {
                    "prop_sec_barier_01a",
                    "prop_sec_barier_02a",
                    "prop_sec_barier_03a",
                    "prop_sec_barier_04a",
                    "prop_sec_barrier_ld_01a",
                    "prop_sec_barrier_ld_02a",
                    "prop_fnclink_03gate5",
                    "prop_gate_airport_01",
                    "prop_gate_docks_ld",
                }
                .Select(x => new Model(x))
                .Where(x => x.IsInCdImage && x.IsValid)
                .Select(x => x.Hash)
                .Distinct()
                .ToArray();
        }

        private static int[] CreateAnimatedBarrierModelHashes()
        {
            return new[]
                {
                    "prop_fnclink_03gate5",
                    "prop_gate_airport_01",
                    "prop_gate_docks_ld",
                }
                .Select(x => new Model(x))
                .Where(x => x.IsInCdImage && x.IsValid)
                .Select(x => x.Hash)
                .Distinct()
                .ToArray();
        }
    }
}