using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Native;

namespace BicycleCity
{
    public class BicycleCity : Script
    {
        int bikesPercentage;
        bool aggressiveDrivers;
        bool aggressiveCyclists;
        bool cyclistsBreakLaws;
        DateTime lastTime;
        string[] availableBicycles = { "BMX", "CRUISER", "FIXTER", "SCORCHER", "TRIBIKE", "TRIBIKE2", "TRIBIKE3" };
        VehicleDrivingFlags aggressiveDrivingStyle = VehicleDrivingFlags.AvoidEmptyVehicles |
                                                     VehicleDrivingFlags.AvoidObjects |
                                                     VehicleDrivingFlags.AvoidPeds |
                                                     VehicleDrivingFlags.AvoidVehicles |
                                                     VehicleDrivingFlags.StopAtTrafficLights;
        VehicleDrivingFlags lawBreakerDrivingStyle = VehicleDrivingFlags.AllowGoingWrongWay |
                                                     VehicleDrivingFlags.AllowMedianCrossing |
                                                     VehicleDrivingFlags.AvoidEmptyVehicles |
                                                     VehicleDrivingFlags.AvoidObjects |
                                                     VehicleDrivingFlags.AvoidVehicles;

        public BicycleCity()
        {
            ScriptSettings settings = ScriptSettings.Load(@".\Scripts\BicycleCity.ini");
            bikesPercentage = settings.GetValue("Main", "BikesPercentage", 50);
            if (bikesPercentage > 100)
                bikesPercentage = 100;
            aggressiveDrivers = settings.GetValue("Main", "AggressiveDrivers", false);
            aggressiveCyclists = settings.GetValue("Main", "AggressiveCyclists", false);
            cyclistsBreakLaws = settings.GetValue("Main", "CyclistsBreakLaws", false);
            lastTime = DateTime.UtcNow;
            Tick += OnTick;
        }

        void OnTick(object sender, EventArgs e)
        {
            if (DateTime.UtcNow >= lastTime.AddSeconds(1))
            {
                Vehicle[] allVehicles = World.GetAllVehicles();
                List<Vehicle> canChange = new List<Vehicle>();
                int bicycles = 0;
                foreach (Vehicle vehicle in allVehicles)
                {
                    if (vehicle.Driver == null || vehicle.Driver.IsPlayer)
                        continue;
                    if (vehicle.Model.IsBicycle)
                        bicycles++;
                    else if (!vehicle.Model.IsTrain && !vehicle.Model.IsBoat &&
                             !vehicle.Model.IsHelicopter && !vehicle.Model.IsPlane &&
                             !Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, vehicle))
                    {
                        canChange.Add(vehicle);
                        if (aggressiveDrivers)
                            Function.Call(Hash.SET_DRIVE_TASK_DRIVING_STYLE, vehicle.Driver, (int)aggressiveDrivingStyle);
                    }
                }
                int toChange = (bicycles + canChange.Count) * bikesPercentage / 100 - bicycles;
                for (int i = 0; i < toChange; i++)
                {
                    Ped driver = canChange[i].Driver;
                    if (driver == null)
                        continue;
                    if (canChange[i].IsInRange(Game.Player.Character.Position, 100f) && canChange[i].IsOnScreen)
                        continue;
                    Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, driver, true, true);
                    driver.AlwaysKeepTask = false;
                    Model newModel;
                    Random random = new Random();
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
                        newVehicle.MarkAsNoLongerNeeded();
                    }
                }
                canChange.Clear();
                lastTime = DateTime.UtcNow;
            }
        }
    }
}
