using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using GTA;
using GTA.Math;
using GTA.Native;

namespace BicycleCity
{
    public class BicycleCity : Script
    {
        private readonly int bikesPercentage;
        private readonly bool aggressiveDrivers;
        private readonly bool aggressiveCyclists;
        private readonly bool cyclistsBreakLaws;
        private readonly bool cheeringCrowds;
        private readonly int cheeringCrowdsSlope;
        private readonly bool stopPedAttacks;
        private readonly bool cantFallFromBike;
        private int lastTime = Environment.TickCount;
        private int lastPaparazzi = Environment.TickCount;
        private readonly List<Ped> fans = new List<Ped>();
        private readonly string[] availableBicycles = { "BMX", "CRUISER", "FIXTER", "SCORCHER", "TRIBIKE", "TRIBIKE2", "TRIBIKE3" };
        private readonly VehicleDrivingFlags aggressiveDrivingStyle = VehicleDrivingFlags.SteerAroundStationaryVehicles |
                                                                      VehicleDrivingFlags.SteerAroundObjects |
                                                                      VehicleDrivingFlags.SteerAroundPeds |
                                                                      VehicleDrivingFlags.SwerveAroundAllVehicles |
                                                                      VehicleDrivingFlags.StopAtTrafficLights;
        private readonly VehicleDrivingFlags lawBreakerDrivingStyle = VehicleDrivingFlags.AllowGoingWrongWay |
                                                                      VehicleDrivingFlags.UseShortCutLinks |
                                                                      VehicleDrivingFlags.SteerAroundStationaryVehicles |
                                                                      VehicleDrivingFlags.SteerAroundObjects |
                                                                      VehicleDrivingFlags.SwerveAroundAllVehicles;

        public BicycleCity()
        {
            ScriptSettings settings = ScriptSettings.Load(@".\Scripts\BicycleCity.ini");
            bikesPercentage = settings.GetValue("Main", "BikesPercentage", 0);
            if (bikesPercentage < 0) bikesPercentage = 0;
            if (bikesPercentage > 100) bikesPercentage = 100;
            aggressiveDrivers = settings.GetValue("Main", "AggressiveDrivers", false);
            aggressiveCyclists = settings.GetValue("Main", "AggressiveCyclists", false);
            cyclistsBreakLaws = settings.GetValue("Main", "CyclistsBreakLaws", false);
            cheeringCrowds = settings.GetValue("Main", "CheeringCrowds", true);
            cheeringCrowdsSlope = settings.GetValue("Main", "CheeringCrowdsSlope", 8);
            stopPedAttacks = settings.GetValue("Main", "StopPedAttacks", false);
            cantFallFromBike = settings.GetValue("Main", "CantFallFromBike", true);
            Tick += OnTick;
            Aborted += OnAbort;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (Environment.TickCount >= lastTime + 1000)
            {
                List<Vehicle> canChange = new List<Vehicle>();
                int bicycles = 0;
                foreach (Vehicle vehicle in World.GetAllVehicles())
                {
                    if (vehicle.Driver != null && vehicle.Driver.IsPlayer)
                        continue;
                    if (vehicle.Model.IsBicycle)
                        bicycles++;
                    else if (!vehicle.Model.IsTrain && !vehicle.Model.IsBoat &&
                             !vehicle.Model.IsHelicopter && !vehicle.Model.IsPlane &&
                             !Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, vehicle) &&
                             !World.GetNearbyVehicles(vehicle.Position, 2f).ToList().Any(x => x.Model.IsBicycle))
                    {
                        canChange.Add(vehicle);
                        if (aggressiveDrivers && vehicle.Driver != null)
                            Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE, vehicle.Driver, (int)aggressiveDrivingStyle);
                    }
                }
                Random random = new Random();
                int toChange = (bicycles + canChange.Count) * bikesPercentage / 100 - bicycles;
                for (int i = 0; i < toChange; i++)
                {
                    Ped driver = canChange[i].Driver;
                    if (canChange[i].IsInRange(Game.Player.Character.Position, 100f) && canChange[i].IsOnScreen)
                        continue;
					if (driver != null)
					{
						Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);
						driver.AlwaysKeepTask = false;
					}
                    Model newModel;
                    newModel = new Model(availableBicycles[random.Next(availableBicycles.Length)]);
                    newModel.Request();
                    if (newModel.IsInCdImage && newModel.IsValid)
                    {
                        while (!newModel.IsLoaded)
                            Wait(10);
                        Vehicle newVehicle = World.CreateVehicle(newModel, canChange[i].Position, canChange[i].Heading);
                        newModel.MarkAsNoLongerNeeded();
                        newVehicle.Mods.CustomPrimaryColor = Color.FromArgb(random.Next(255), random.Next(255), random.Next(255));
                        newVehicle.MaxSpeed = 10;
                        canChange[i].Delete();
                        if (driver != null)
						{
							driver.SetIntoVehicle(newVehicle, VehicleSeat.Driver);
							int drivingStyle;
							if (cyclistsBreakLaws)
								drivingStyle = (int)lawBreakerDrivingStyle;
							else if (aggressiveCyclists)
								drivingStyle = (int)aggressiveDrivingStyle;
							else
								drivingStyle = (int)DrivingStyle.Normal;
							Function.Call(Hash.TASK_VEHICLE_DRIVE_WANDER, driver, newVehicle, (float)random.Next(4, 8), drivingStyle);
							Function.Call(Hash.SET_PED_KEEP_TASK, driver, true);
							driver.MarkAsNoLongerNeeded();
						}
                        newVehicle.MarkAsNoLongerNeeded();
                    }
                }
                canChange.Clear();

                if (cheeringCrowds)
                {
                    Vector3 point1 = Game.Player.Character.Position;
                    Vector3 point2 = Game.Player.Character.Position + Game.Player.Character.ForwardVector * 2f;
                    float slope = (World.GetGroundHeight(point2) - World.GetGroundHeight(point1)) / 2f;

                    if (slope > cheeringCrowdsSlope / 100f && fans.Count < 100 && Game.Player.Character.CurrentVehicle?.Speed > 0)
                    {
                        NativeVector3 spawnPoint;
                        point2 = Game.Player.Character.ForwardVector;
                        unsafe
                        {
                            Function.Call(Hash.FIND_SPAWN_POINT_IN_DIRECTION, point1.X, point1.Y, point1.Z, point2.X, point2.Y, point2.Z, 100f, &spawnPoint);
                        }
                        var position = World.GetNextPositionOnSidewalk(spawnPoint);

                        if (Environment.TickCount >= lastPaparazzi + 10000)
                        {
                            Model pModel;
                            pModel = new Model("a_m_m_paparazzi_01");
                            pModel.Request();
                            if (pModel.IsInCdImage && pModel.IsValid)
                            {
                                while (!pModel.IsLoaded)
                                    Wait(10);
                                Ped paparazzi = World.CreatePed(pModel, position);
                                pModel.MarkAsNoLongerNeeded();
                                paparazzi.Task.StartScenario("WORLD_HUMAN_PAPARAZZI", 0f);
                                fans.Add(paparazzi);
                            }
                            lastPaparazzi = Environment.TickCount;
                        }
                        else
                        {
                            Ped fan = World.CreateRandomPed(position);
                            fan.Task.StartScenario("WORLD_HUMAN_CHEERING", 0f);
                            fans.Add(fan);
                        }
                    }

                    foreach (Ped fan in fans.ToArray())
                    {
                        if (fan != null)
                        {
                            if (fan.Position.DistanceTo(Game.Player.Character.Position) > 150f || IsEnemy(fan))
                            {
                                fan.Delete();
                                fans.Remove(fan);
                            }
                        }
                        else
                            fans.Remove(fan);
                    }
                }

                if (stopPedAttacks)
                    foreach (Ped ped in World.GetNearbyPeds(Game.Player.Character, 100f))
                        if (IsEnemy(ped))
                            ped.Delete();

                lastTime = Environment.TickCount;
            }

            if (cheeringCrowds)
                foreach (Ped fan in fans)
                    if (fan != null && !fan.IsRunning)
                        fan.Heading = (Game.Player.Character.Position - fan.Position).ToHeading();

            if (cantFallFromBike)
                Function.Call(Hash.SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE, Game.Player.Character, 1);
        }

        private bool IsEnemy(Ped ped)
        {
            return (ped.GetRelationshipWithPed(Game.Player.Character) == Relationship.Hate && ped.IsHuman) || ped.IsInCombat || ped.IsInMeleeCombat || ped.IsShooting;
        }

        private void OnAbort(object sender, EventArgs e)
        {
            Tick -= OnTick;

            if (cheeringCrowds)
            {
                foreach (Ped fan in fans)
                    fan.Delete();
                fans.Clear();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    internal struct NativeVector3
    {
        [FieldOffset(0x00)]
        internal float X;
        [FieldOffset(0x08)]
        internal float Y;
        [FieldOffset(0x10)]
        internal float Z;

        public static implicit operator Vector3(NativeVector3 value) => new Vector3(value.X, value.Y, value.Z);
    }
}
