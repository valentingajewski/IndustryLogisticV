using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace LSOL.Config
{
    // Explicitly telling the game engine this is a script
    public sealed class GateOpener : Script
    {
        public GateOpener()
        {
            // This will beep when the game safely loads the script
            System.Media.SystemSounds.Beep.Play();
            
            Tick += OnTick;
            Interval = 500; // Built-in SHVDN timer (runs every 500ms instead of using DateTime)
        }

        private void OnTick(object sender, EventArgs e)
        {
            Ped playerPed = Game.Player.Character;
            if (playerPed == null || !playerPed.Exists()) return;

            Vector3 playerPos = playerPed.Position;
            
            // Grab nearby objects
            Prop[] nearbyProps = World.GetNearbyProps(playerPos, 30.0f);

            foreach (Prop prop in nearbyProps)
            {
                if (prop.Exists())
                {
                    uint modelHash = (uint)prop.Model.Hash;

                    // Native call to force doors/gates open by their hash and coordinates
                    Function.Call(Hash.SET_STATE_OF_CLOSEST_DOOR_OF_TYPE, 
                        modelHash, 
                        prop.Position.X, 
                        prop.Position.Y, 
                        prop.Position.Z, 
                        false, // Locked state (false = unlocked)
                        0.0f,  // Open ratio (0.0 = fully open for business)
                        false
                    );
                }
            }
        }
    }
}