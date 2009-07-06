using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Lidgren.Network;
using Lidgren.Network.Xna;
using Microsoft.Xna.Framework;

//Contains functions related to the state of the gameworld

namespace Infiniminer
{
    public partial class InfiniminerServer
    {
        public BlockType[, ,] blockList = null;    // In game coordinates, where Y points up.
        PlayerTeam[, ,] blockCreatorTeam = null;
        const int MAPSIZE = 64;


        List<string> beaconIDList = new List<string>();
        Dictionary<Vector3, Beacon> beaconList = new Dictionary<Vector3, Beacon>();
        Random randGen = new Random();
        public string _GenerateBeaconID()
        {
            string id = "K";
            for (int i = 0; i < 3; i++)
                id += (char)randGen.Next(48, 58);
            return id;
        }
        public string GenerateBeaconID()
        {
            string newId = _GenerateBeaconID();
            while (beaconIDList.Contains(newId))
                newId = _GenerateBeaconID();
            beaconIDList.Add(newId);
            return newId;
        }

        public void SetBlock(ushort x, ushort y, ushort z, BlockType blockType, PlayerTeam team)
        {
            if (x <= 0 || y <= 0 || z <= 0 || (int)x >= MAPSIZE - 1 || (int)y >= MAPSIZE - 1 || (int)z >= MAPSIZE - 1)
                return;

            if (blockType == BlockType.BeaconRed || blockType == BlockType.BeaconBlue)
            {
                Beacon newBeacon = new Beacon();
                newBeacon.ID = GenerateBeaconID();
                newBeacon.Team = blockType == BlockType.BeaconRed ? PlayerTeam.Red : PlayerTeam.Blue;
                beaconList[new Vector3(x, y, z)] = newBeacon;
                SendSetBeacon(new Vector3(x, y + 1, z), newBeacon.ID, newBeacon.Team);
            }

            if (blockType == BlockType.None && (blockList[x, y, z] == BlockType.BeaconRed || blockList[x, y, z] == BlockType.BeaconBlue))
            {
                if (beaconList.ContainsKey(new Vector3(x, y, z)))
                    beaconList.Remove(new Vector3(x, y, z));
                SendSetBeacon(new Vector3(x, y + 1, z), "", PlayerTeam.None);
            }

            blockList[x, y, z] = blockType;
            blockCreatorTeam[x, y, z] = team;

            // x, y, z, type, all bytes
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.BlockSet);
            msgBuffer.Write((byte)x);
            msgBuffer.Write((byte)y);
            msgBuffer.Write((byte)z);
            msgBuffer.Write((byte)blockType);
            foreach (IClient player in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                    player.AddQueMsg(msgBuffer, NetChannel.ReliableUnordered);

            if (blockType == BlockType.Lava)
                lavaBlockCount += 1;

            //ConsoleWrite("BLOCKSET: " + x + " " + y + " " + z + " " + blockType.ToString());
        }

        public int newMap()
        {
            // Create our block world, translating the coordinates out of the cave generator (where Z points down)
            BlockType[, ,] worldData = CaveGenerator.GenerateCaveSystem(MAPSIZE, includeLava, oreFactor);
            blockList = new BlockType[MAPSIZE, MAPSIZE, MAPSIZE];
            blockCreatorTeam = new PlayerTeam[MAPSIZE, MAPSIZE, MAPSIZE];
            for (ushort i = 0; i < MAPSIZE; i++)
                for (ushort j = 0; j < MAPSIZE; j++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                    {
                        blockList[i, (ushort)(MAPSIZE - 1 - k), j] = worldData[i, j, k];
                        blockCreatorTeam[i, j, k] = PlayerTeam.None;
                    }
            for (int i = 0; i < MAPSIZE * 2; i++)
                DoLavaStuff();
            return lavaBlockCount;
        }

        public double Get3DDistance(int x1, int y1, int z1, int x2, int y2, int z2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;
            int dz = z2 - z1;
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return distance;
        }

        public string GetExplosionPattern(int n)
        {
            string output = "";
            int radius = (int)Math.Ceiling((double)varGetI("explosionradius"));
            int size = radius * 2 + 1;
            int center = radius; //Not adding one because arrays start from 0
            for (int z = n; z == n && z < size; z++)
            {
                ConsoleWrite("Z" + z + ": ");
                output += "Z" + z + ": ";
                for (int x = 0; x < size; x++)
                {
                    string output1 = "";
                    for (int y = 0; y < size; y++)
                    {
                        output1 += tntExplosionPattern[x, y, z] ? "1, " : "0, ";
                    }
                    ConsoleWrite(output1);
                }
                output += "\n";
            }
            return "";
        }

        public void CalculateExplosionPattern()
        {
            int radius = (int)Math.Ceiling((double)varGetI("explosionradius"));
            int size = radius * 2 + 1;
            tntExplosionPattern = new bool[size, size, size];
            int center = radius; //Not adding one because arrays start from 0
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                    {
                        if (x == y && y == z && z == center)
                            tntExplosionPattern[x, y, z] = true;
                        else
                        {
                            double distance = Get3DDistance(center, center, center, x, y, z);//Use center of blocks
                            if (distance <= (double)varGetI("explosionradius"))
                                tntExplosionPattern[x, y, z] = true;
                            else
                                tntExplosionPattern[x, y, z] = false;
                        }
                    }
        }

        public void DepositForPlayers()
        {
            foreach (IClient p in playerList.Values)
            {
                if (p.Position.Y > MAPSIZE - Defines.GROUND_LEVEL)
                    DepositCash(p);
            }

            if (varGetB("sandbox"))
                return;
            if (teamCashBlue >= winningCashAmount && winningTeam == PlayerTeam.None)
                winningTeam = PlayerTeam.Blue;
            if (teamCashRed >= winningCashAmount && winningTeam == PlayerTeam.None)
                winningTeam = PlayerTeam.Red;
        }

        public void DoLavaStuff()
        {
            bool[, ,] flowSleep = new bool[MAPSIZE, MAPSIZE, MAPSIZE]; //if true, do not calculate this turn

            for (ushort i = 0; i < MAPSIZE; i++)
                for (ushort j = 0; j < MAPSIZE; j++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                        flowSleep[i, j, k] = false;

            for (ushort i = 0; i < MAPSIZE; i++)
                for (ushort j = 0; j < MAPSIZE; j++)
                    for (ushort k = 0; k < MAPSIZE; k++)
                        if (blockList[i, j, k] == BlockType.Lava && !flowSleep[i, j, k])
                        {
                            // RULES FOR LAVA EXPANSION:
                            // if the block below is lava, do nothing
                            // if the block below is empty space, add lava there
                            // if the block below is something solid (or insane lava is on), add lava to the sides
                            // if shock block spreading is enabled and there is a schock block in any direction...
                            // if road block above and roadabsorbs is enabled then contract
                            if (varGetB("sspreads"))
                            {
                                BlockType typeAbove = ((int)j == MAPSIZE - 1) ? BlockType.None : blockList[i, j + 1, k];
                                if (i > 0 && blockList[i - 1, j, k] == BlockType.Shock)
                                {
                                    SetBlock((ushort)(i - 1), j, k, BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i - 1, j, k] = true;
                                }
                                if (k > 0 && blockList[i, j, k - 1] == BlockType.Shock)
                                {
                                    SetBlock(i, j, (ushort)(k - 1), BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i, j, k - 1] = true;
                                }
                                if ((int)i < MAPSIZE - 1 && blockList[i + 1, j, k] == BlockType.Shock)
                                {
                                    SetBlock((ushort)(i + 1), j, k, BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i + 1, j, k] = true;
                                }
                                if ((int)k < MAPSIZE - 1 && blockList[i, j, k + 1] == BlockType.Shock)
                                {
                                    SetBlock(i, j, (ushort)(k + 1), BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i, j, k + 1] = true;
                                }
                                if (typeAbove == BlockType.Shock) //Spread up
                                {
                                    SetBlock(i, (ushort)(j + 1), k, BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i, j + 1, k] = true;
                                }
                                //Don't spread down...
                            }
                            if (varGetB("roadabsorbs"))
                            {
                                BlockType typeAbove = ((int)j == MAPSIZE - 1) ? BlockType.None : blockList[i, j + 1, k];
                                if (typeAbove == BlockType.Road)
                                {
                                    SetBlock(i, j, k, BlockType.Road, PlayerTeam.None);
                                    flowSleep[i, j, k] = true;
                                }
                            }
                            BlockType typeBelow = (j == 0) ? BlockType.Lava : blockList[i, j - 1, k];
                            if (typeBelow == BlockType.None)
                            {
                                if (j > 0)
                                {
                                    SetBlock(i, (ushort)(j - 1), k, BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i, j - 1, k] = true;
                                }
                            }
                            else if (typeBelow != BlockType.Lava || varGetB("insanelava"))
                            {
                                if (i > 0 && blockList[i - 1, j, k] == BlockType.None)
                                {
                                    SetBlock((ushort)(i - 1), j, k, BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i - 1, j, k] = true;
                                }
                                if (k > 0 && blockList[i, j, k - 1] == BlockType.None)
                                {
                                    SetBlock(i, j, (ushort)(k - 1), BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i, j, k - 1] = true;
                                }
                                if ((int)i < MAPSIZE - 1 && blockList[i + 1, j, k] == BlockType.None)
                                {
                                    SetBlock((ushort)(i + 1), j, k, BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i + 1, j, k] = true;
                                }
                                if ((int)k < MAPSIZE - 1 && blockList[i, j, k + 1] == BlockType.None)
                                {
                                    SetBlock(i, j, (ushort)(k + 1), BlockType.Lava, PlayerTeam.None);
                                    flowSleep[i, j, k + 1] = true;
                                }
                            }
                        }
        }

        public BlockType BlockAtPoint(Vector3 point)
        {
            ushort x = (ushort)point.X;
            ushort y = (ushort)point.Y;
            ushort z = (ushort)point.Z;
            if (x <= 0 || y <= 0 || z <= 0 || (int)x >= MAPSIZE - 1 || (int)y >= MAPSIZE - 1 || (int)z >= MAPSIZE - 1)
                return BlockType.None;
            return blockList[x, y, z];
        }

        public bool RayCollision(Vector3 startPosition, Vector3 rayDirection, float distance, int searchGranularity, ref Vector3 hitPoint, ref Vector3 buildPoint)
        {
            Vector3 testPos = startPosition;
            Vector3 buildPos = startPosition;
            for (int i = 0; i < searchGranularity; i++)
            {
                testPos += rayDirection * distance / searchGranularity;
                BlockType testBlock = BlockAtPoint(testPos);
                if (testBlock != BlockType.None)
                {
                    hitPoint = testPos;
                    buildPoint = buildPos;
                    return true;
                }
                buildPos = testPos;
            }
            return false;
        }

        public void UsePickaxe(IClient player, Vector3 playerPosition, Vector3 playerHeading)
        {
            player.QueueAnimationBreak = true;

            // Figure out what we're hitting.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!RayCollision(playerPosition, playerHeading, 2, 10, ref hitPoint, ref buildPoint))
                return;
            ushort x = (ushort)hitPoint.X;
            ushort y = (ushort)hitPoint.Y;
            ushort z = (ushort)hitPoint.Z;

            // Figure out what the result is.
            bool removeBlock = false;
            uint giveOre = 0;
            uint giveCash = 0;
            uint giveWeight = 0;
            InfiniminerSound sound = InfiniminerSound.DigDirt;

            switch (BlockAtPoint(hitPoint))
            {
                case BlockType.Lava:
                    if (varGetB("minelava"))
                    {
                        removeBlock = true;
                        sound = InfiniminerSound.DigDirt;
                    }
                    break;
                case BlockType.Dirt:
                case BlockType.DirtSign:
                    removeBlock = true;
                    sound = InfiniminerSound.DigDirt;
                    break;

                case BlockType.Ore:
                    removeBlock = true;
                    giveOre = 20;
                    sound = InfiniminerSound.DigMetal;
                    break;

                case BlockType.Gold:
                    removeBlock = true;
                    giveWeight = 1;
                    giveCash = 100;
                    sound = InfiniminerSound.DigMetal;
                    break;

                case BlockType.Diamond:
                    removeBlock = true;
                    giveWeight = 1;
                    giveCash = 1000;
                    sound = InfiniminerSound.DigMetal;
                    break;
            }

            if (giveOre > 0)
            {
                if (player.Ore < player.OreMax)
                {
                    player.Ore = Math.Min(player.Ore + giveOre, player.OreMax);
                    SendResourceUpdate(player);
                }
            }

            if (giveWeight > 0)
            {
                if (player.Weight < player.WeightMax)
                {
                    player.Weight = Math.Min(player.Weight + giveWeight, player.WeightMax);
                    player.Cash += giveCash;
                    SendResourceUpdate(player);
                }
                else
                    removeBlock = false;
            }

            if (removeBlock)
            {
                SetBlock(x, y, z, BlockType.None, PlayerTeam.None);
                PlaySound(sound, player.Position);
            }
        }

        //private bool LocationNearBase(ushort x, ushort y, ushort z)
        //{
        //    for (int i=0; i<MAPSIZE; i++)
        //        for (int j=0; j<MAPSIZE; j++)
        //            for (int k = 0; k < MAPSIZE; k++)
        //                if (blockList[i, j, k] == BlockType.HomeBlue || blockList[i, j, k] == BlockType.HomeRed)
        //                {
        //                    double dist = Math.Sqrt(Math.Pow(x - i, 2) + Math.Pow(y - j, 2) + Math.Pow(z - k, 2));
        //                    if (dist < 3)
        //                        return true;
        //                }
        //    return false;
        //}

        public void UseConstructionGun(IClient player, Vector3 playerPosition, Vector3 playerHeading, BlockType blockType)
        {
            bool actionFailed = false;

            // If there's no surface within range, bail.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!RayCollision(playerPosition, playerHeading, 6, 25, ref hitPoint, ref buildPoint))
                actionFailed = true;

            // If the block is too expensive, bail.
            uint blockCost = BlockInformation.GetCost(blockType);
            if (varGetB("sandbox") && blockCost <= player.OreMax)
                blockCost = 0;
            if (blockCost > player.Ore)
                actionFailed = true;

            // If there's someone there currently, bail.
            ushort x = (ushort)buildPoint.X;
            ushort y = (ushort)buildPoint.Y;
            ushort z = (ushort)buildPoint.Z;
            foreach (IClient p in playerList.Values)
            {
                if ((int)p.Position.X == x && (int)p.Position.Z == z && ((int)p.Position.Y == y || (int)p.Position.Y - 1 == y))
                    actionFailed = true;
            }

            // If it's out of bounds, bail.
            if (x <= 0 || y <= 0 || z <= 0 || (int)x >= MAPSIZE - 1 || (int)y >= MAPSIZE - 1 || (int)z >= MAPSIZE - 1)
                actionFailed = true;

            // If it's near a base, bail.
            //if (LocationNearBase(x, y, z))
            //    actionFailed = true;

            // If it's lava, don't let them build off of lava.
            if (blockList[(ushort)hitPoint.X, (ushort)hitPoint.Y, (ushort)hitPoint.Z] == BlockType.Lava)
                actionFailed = true;

            if (actionFailed)
            {
                // Decharge the player's gun.
                TriggerConstructionGunAnimation(player, -0.2f);
            }
            else
            {
                // Fire the player's gun.
                TriggerConstructionGunAnimation(player, 0.5f);

                // Build the block.
                SetBlock(x, y, z, blockType, player.Team);
                player.Ore -= blockCost;
                SendResourceUpdate(player);

                // Play the sound.
                PlaySound(InfiniminerSound.ConstructionGun, player.Position);

                // If it's an explosive block, add it to our list.
                if (blockType == BlockType.Explosive)
                    player.ExplosiveList.Add(buildPoint);
            }
        }

        public void UseDeconstructionGun(IClient player, Vector3 playerPosition, Vector3 playerHeading)
        {
            bool actionFailed = false;

            // If there's no surface within range, bail.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!RayCollision(playerPosition, playerHeading, 6, 25, ref hitPoint, ref buildPoint))
                actionFailed = true;
            ushort x = (ushort)hitPoint.X;
            ushort y = (ushort)hitPoint.Y;
            ushort z = (ushort)hitPoint.Z;

            // If this is another team's block, bail.
            if (blockCreatorTeam[x, y, z] != player.Team)
                actionFailed = true;

            BlockType blockType = blockList[x, y, z];
            if (!(blockType == BlockType.SolidBlue ||
                blockType == BlockType.SolidRed ||
                blockType == BlockType.BankBlue ||
                blockType == BlockType.BankRed ||
                blockType == BlockType.Jump ||
                blockType == BlockType.Ladder ||
                blockType == BlockType.Road ||
                blockType == BlockType.Shock ||
                blockType == BlockType.BeaconRed ||
                blockType == BlockType.BeaconBlue ||
                blockType == BlockType.TransBlue ||
                blockType == BlockType.TransRed))
                actionFailed = true;

            if (actionFailed)
            {
                // Decharge the player's gun.
                TriggerConstructionGunAnimation(player, -0.2f);
            }
            else
            {
                // Fire the player's gun.
                TriggerConstructionGunAnimation(player, 0.5f);

                // Remove the block.
                SetBlock(x, y, z, BlockType.None, PlayerTeam.None);
                PlaySound(InfiniminerSound.ConstructionGun, player.Position);
            }
        }

        public void TriggerConstructionGunAnimation(IClient player, float animationValue)
        {
            if (player.NetConn.Status != NetConnectionStatus.Connected)
                return;

            // ore, cash, weight, max ore, max weight, team ore, red cash, blue cash, all uint
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TriggerConstructionGunAnimation);
            msgBuffer.Write(animationValue);
            player.AddQueMsg(msgBuffer, NetChannel.ReliableInOrder1);
        }

        public void UseSignPainter(IClient player, Vector3 playerPosition, Vector3 playerHeading)
        {
            // If there's no surface within range, bail.
            Vector3 hitPoint = Vector3.Zero;
            Vector3 buildPoint = Vector3.Zero;
            if (!RayCollision(playerPosition, playerHeading, 4, 25, ref hitPoint, ref buildPoint))
                return;
            ushort x = (ushort)hitPoint.X;
            ushort y = (ushort)hitPoint.Y;
            ushort z = (ushort)hitPoint.Z;

            if (blockList[x, y, z] == BlockType.Dirt)
            {
                SetBlock(x, y, z, BlockType.DirtSign, PlayerTeam.None);
                PlaySound(InfiniminerSound.ConstructionGun, player.Position);
            }
            else if (blockList[x, y, z] == BlockType.DirtSign)
            {
                SetBlock(x, y, z, BlockType.Dirt, PlayerTeam.None);
                PlaySound(InfiniminerSound.ConstructionGun, player.Position);
            }
        }

        public void ExplosionEffectAtPoint(int x, int y, int z)
        {
            // Send off the explosion to clients.
            NetBuffer msgBuffer = netServer.CreateBuffer();
            msgBuffer.Write((byte)InfiniminerMessage.TriggerExplosion);
            msgBuffer.Write(new Vector3(x, y, z));
            foreach (IClient player in playerList.Values)
                //if (netConn.Status == NetConnectionStatus.Connected)
                player.AddQueMsg(msgBuffer,  NetChannel.ReliableUnordered);
            //Or not, there's no dedicated function for this effect >:(
        }

        public void DetonateAtPoint(int x, int y, int z)
        {
            // Remove the block that is detonating.
            SetBlock((ushort)(x), (ushort)(y), (ushort)(z), BlockType.None, PlayerTeam.None);

            // Remove this from any explosive lists it may be in.
            foreach (IClient p in playerList.Values)
                p.ExplosiveList.Remove(new Vector3(x, y, z));

            // Detonate the block.
            if (!varGetB("stnt"))
            {
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dz = -2; dz <= 2; dz++)
                        {
                            // Check that this is a sane block position.
                            if (x + dx <= 0 || y + dy <= 0 || z + dz <= 0 || x + dx >= MAPSIZE - 1 || y + dy >= MAPSIZE - 1 || z + dz >= MAPSIZE - 1)
                                continue;

                            // Chain reactions!
                            if (blockList[x + dx, y + dy, z + dz] == BlockType.Explosive)
                                DetonateAtPoint(x + dx, y + dy, z + dz);

                            // Detonation of normal blocks.
                            bool destroyBlock = false;
                            switch (blockList[x + dx, y + dy, z + dz])
                            {
                                case BlockType.Rock:
                                case BlockType.Dirt:
                                case BlockType.DirtSign:
                                case BlockType.Ore:
                                case BlockType.SolidRed:
                                case BlockType.SolidBlue:
                                case BlockType.TransRed:
                                case BlockType.TransBlue:
                                case BlockType.Ladder:
                                case BlockType.Shock:
                                case BlockType.Jump:
                                case BlockType.Explosive:
                                case BlockType.Lava:
                                case BlockType.Road:
                                    destroyBlock = true;
                                    break;
                            }
                            if (destroyBlock)
                                SetBlock((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                        }
            }
            else
            {
                int radius = (int)Math.Ceiling((double)varGetI("explosionradius"));
                int size = radius * 2 + 1;
                int center = radius + 1;
                //ConsoleWrite("Radius: " + radius + ", Size: " + size + ", Center: " + center);
                for (int dx = -center + 1; dx < center; dx++)
                    for (int dy = -center + 1; dy < center; dy++)
                        for (int dz = -center + 1; dz < center; dz++)
                        {
                            if (tntExplosionPattern[dx + center - 1, dy + center - 1, dz + center - 1]) //Warning, code duplication ahead!
                            {
                                // Check that this is a sane block position.
                                if (x + dx <= 0 || y + dy <= 0 || z + dz <= 0 || x + dx >= MAPSIZE - 1 || y + dy >= MAPSIZE - 1 || z + dz >= MAPSIZE - 1)
                                    continue;

                                // Chain reactions!
                                if (blockList[x + dx, y + dy, z + dz] == BlockType.Explosive)
                                    DetonateAtPoint(x + dx, y + dy, z + dz);

                                // Detonation of normal blocks.
                                bool destroyBlock = false;
                                switch (blockList[x + dx, y + dy, z + dz])
                                {
                                    case BlockType.Rock:
                                    case BlockType.Dirt:
                                    case BlockType.DirtSign:
                                    case BlockType.Ore:
                                    case BlockType.SolidRed:
                                    case BlockType.SolidBlue:
                                    case BlockType.TransRed:
                                    case BlockType.TransBlue:
                                    case BlockType.Ladder:
                                    case BlockType.Shock:
                                    case BlockType.Jump:
                                    case BlockType.Explosive:
                                    case BlockType.Lava:
                                    case BlockType.Road:
                                        destroyBlock = true;
                                        break;
                                }
                                if (destroyBlock)
                                    SetBlock((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz), BlockType.None, PlayerTeam.None);
                            }
                        }
            }
            ExplosionEffectAtPoint(x, y, z);
        }

        public void UseDetonator(IClient player)
        {
            while (player.ExplosiveList.Count > 0)
            {
                Vector3 blockPos = player.ExplosiveList[0];
                ushort x = (ushort)blockPos.X;
                ushort y = (ushort)blockPos.Y;
                ushort z = (ushort)blockPos.Z;

                if (blockList[x, y, z] != BlockType.Explosive)
                    player.ExplosiveList.RemoveAt(0);
                else if (!varGetB("tnt"))
                {
                    player.ExplosiveList.RemoveAt(0);
                    ExplosionEffectAtPoint(x, y, z);
                    // Remove the block that is detonating.
                    SetBlock(x, y, z, BlockType.None, PlayerTeam.None);
                }
                else
                    DetonateAtPoint(x, y, z);
            }
        }

        public void DepositOre(IClient player)
        {
            uint depositAmount = Math.Min(50, player.Ore);
            player.Ore -= depositAmount;
            if (player.Team == PlayerTeam.Red)
                teamOreRed = Math.Min(teamOreRed + depositAmount, 9999);
            else
                teamOreBlue = Math.Min(teamOreBlue + depositAmount, 9999);
        }

        public void WithdrawOre(IClient player)
        {
            if (player.Team == PlayerTeam.Red)
            {
                uint withdrawAmount = Math.Min(player.OreMax - player.Ore, Math.Min(50, teamOreRed));
                player.Ore += withdrawAmount;
                teamOreRed -= withdrawAmount;
            }
            else
            {
                uint withdrawAmount = Math.Min(player.OreMax - player.Ore, Math.Min(50, teamOreBlue));
                player.Ore += withdrawAmount;
                teamOreBlue -= withdrawAmount;
            }
        }

        public void DepositCash(IClient player)
        {
            if (player.Cash <= 0)
                return;

            player.Score += player.Cash;

            if (!varGetB("sandbox"))
            {
                if (player.Team == PlayerTeam.Red)
                    teamCashRed += player.Cash;
                else
                    teamCashBlue += player.Cash;
                SendServerMessage("SERVER: " + player.Handle + " HAS EARNED $" + player.Cash + " FOR THE " + GetTeamName(player.Team) + " TEAM!");
            }

            PlaySound(InfiniminerSound.CashDeposit, player.Position);
            ConsoleWrite("DEPOSIT_CASH: " + player.Handle + ", " + player.Cash);

            player.Cash = 0;
            player.Weight = 0;

            foreach (IClient p in playerList.Values)
                SendResourceUpdate(p);
        }
    }
}