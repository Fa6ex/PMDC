﻿using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using PMDC.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// A monster house that takes the form of a booby-trapped chest.
    /// Once opened, items spill out, the walls lock down, and monsters appear.
    /// All must be defeated in order to unlock.
    /// It could also just be a normal chest.
    /// This step chooses an existing room to put the house in.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class ChestStep<T> : MonsterHouseBaseStep<T> where T : ListMapGenContext
    {
        /// <summary>
        /// Determines if this is actually a monster house and not just a chest with treasure.
        /// </summary>
        public bool Ambush;

        public ChestStep() : base()
        {
            Filters = new List<BaseRoomFilter>();
        }
        public ChestStep(bool ambush, List<BaseRoomFilter> filters)
        {
            Ambush = ambush;
            Filters = filters;
        }
        public ChestStep(ChestStep<T> other) : base(other)
        {
            Ambush = other.Ambush;
            Filters = new List<BaseRoomFilter>();
            Filters.AddRange(other.Filters);
        }
        public override MonsterHouseBaseStep<T> CreateNew() { return new ChestStep<T>(this); }

        /// <summary>
        /// Used to filter out unwanted rooms to be used for this monster house.
        /// </summary>
        public List<BaseRoomFilter> Filters { get; set; }

        public override void Apply(T map)
        {
            Grid.LocTest checkBlock = (Loc testLoc) =>
            {
                return (!map.RoomTerrain.TileEquivalent(map.GetTile(testLoc)) || map.HasTileEffect(testLoc));
            };

            //choose a room to put the chest in
            //do not choose a room that would cause disconnection of the floor
            List<int> possibleRooms = new List<int>();
            for (int ii = 0; ii < map.RoomPlan.RoomCount; ii++)
            {
                FloorRoomPlan testPlan = map.RoomPlan.GetRoomPlan(ii);
                //if (map.RoomPlan.IsChokePoint(new RoomHallIndex(ii, false)))
                //    continue;
                if (!BaseRoomFilter.PassesAllFilters(testPlan, this.Filters))
                    continue;

                //also do not choose a room that contains the end stairs
                IViewPlaceableGenContext<MapGenExit> exitMap = (IViewPlaceableGenContext<MapGenExit>)map;
                if (map.RoomPlan.InBounds(testPlan.RoomGen.Draw, exitMap.GetLoc(0)))
                    continue;

                possibleRooms.Add(ii);
            }

            if (possibleRooms.Count == 0)
                return;

            List<Loc> freeTiles = new List<Loc>();
            IRoomGen room = null;

            while (possibleRooms.Count > 0)
            {
                int chosenRoom = map.Rand.Next(possibleRooms.Count);
                room = map.RoomPlan.GetRoom(possibleRooms[chosenRoom]);

                //get all places that the chest is eligible
                freeTiles = Grid.FindTilesInBox(room.Draw.Start, room.Draw.Size, (Loc testLoc) =>
                {
                    Loc frontLoc = testLoc + Dir8.Down.GetLoc();
                    if (map.RoomTerrain.TileEquivalent(map.GetTile(testLoc)) && !map.HasTileEffect(testLoc) &&
                        map.RoomTerrain.TileEquivalent(map.GetTile(frontLoc)) && !map.HasTileEffect(frontLoc) &&
                        (map.GetPostProc(testLoc).Status & (PostProcType.Panel | PostProcType.Item)) == PostProcType.None)
                    {
                        if (Grid.GetForkDirs(testLoc, checkBlock, checkBlock).Count < 2)
                        {
                            foreach (MapItem item in map.Items)
                            {
                                if (item.TileLoc == testLoc)
                                    return false;
                            }
                            foreach (Team team in map.AllyTeams)
                            {
                                foreach (Character testChar in team.EnumerateChars())
                                {
                                    if (testChar.CharLoc == testLoc)
                                        return false;
                                }
                            }
                            foreach (Team team in map.MapTeams)
                            {
                                foreach (Character testChar in team.EnumerateChars())
                                {
                                    if (testChar.CharLoc == testLoc)
                                        return false;
                                }
                            }
                            return true;
                        }
                    }
                    return false;
                });
                if (freeTiles.Count > 0)
                    break;
                possibleRooms.RemoveAt(chosenRoom);
            }

            //can't find any free tile in any room, return
            if (freeTiles.Count == 0)
                return;

            if (!ItemThemes.CanPick)
                return;
            //choose which item theme to work with
            ItemTheme chosenItemTheme = ItemThemes.Pick(map.Rand);

            //the item spawn list in this class dictates the items available for spawning
            //it will be queried for items that match the theme selected
            List<MapItem> chosenItems = chosenItemTheme.GenerateItems(map, Items);

            if (chosenItems.Count == 0)
                return;

            int randIndex = map.Rand.Next(freeTiles.Count);
            Loc loc = freeTiles[randIndex];


            EffectTile spawnedChest = new EffectTile("chest_full", true);
            spawnedChest.TileStates.Set(new UnlockState("key"));

            if (Ambush && MobThemes.CanPick)
            {

                spawnedChest.Danger = true;
                //the mob theme will be selected randomly
                MobTheme chosenMobTheme = MobThemes.Pick(map.Rand);

                //the mobs in this class are the ones that would be available when the game wants to spawn things outside of the floor's spawn list
                //it will be queried for monsters that match the theme provided
                List<MobSpawn> chosenMobs = chosenMobTheme.GenerateMobs(map, Mobs);


                MobSpawnState mobSpawn = new MobSpawnState();
                foreach (MobSpawn mob in chosenMobs)
                {
                    MobSpawn copyMob = mob.Copy();
                    if (map.Rand.Next(ALT_COLOR_ODDS) == 0)
                        copyMob.BaseForm.Skin = "shiny";
                    mobSpawn.Spawns.Add(copyMob);
                }
                spawnedChest.TileStates.Set(mobSpawn);
            }
            
            ItemSpawnState itemSpawn = new ItemSpawnState();
            itemSpawn.Spawns = chosenItems;
            spawnedChest.TileStates.Set(itemSpawn);

            Rect wallBounds = new Rect(room.Draw.X - 1, room.Draw.Y - 1, room.Draw.Size.X + 2, room.Draw.Size.Y + 2);
            spawnedChest.TileStates.Set(new BoundsState(wallBounds));

            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(loc, spawnedChest);
            map.GetPostProc(loc).Status |= PostProcType.Panel;
            map.GetPostProc(loc).Status |= PostProcType.Item;

            GenContextDebug.DebugProgress("Placed Chest");
        }

        public override string ToString()
        {
            return "Chest Step" + (Ambush ? " Ambush" : "");
        }
    }
}
