using Sandbox;
using System;
using System.Collections.Generic;

namespace GGame;

public class WorldGen {
    public static WorldGen Cur {get; set;}
    
    public WorldGen() {
        if (Cur is not null) return;
        Cur = this;
    }

    [ConCmd.Server("gen_world")]
    public static async void GenerateLevelCMD() {
        await Cur.GenerateLevel(12, 10, 0, false);
    }

    public async System.Threading.Tasks.Task GenerateLevel(int len, int wid, int depth, bool debug) {
        Game.AssertServer();
        Log.Info("Generating World");

        foreach (Entity ent in Entity.All) {
            if (ent.Tags.Has("generated")) ent.Delete();
        }

        World lvl = new() {
            length = len,
            width = wid,
            depth = depth,
            wallType = 0,
            tiles = FillTilesEmpty(len, wid),
        };

        len -= 1;
        wid -= 1;

        // *
        // * grid stage
        // *
        Vector2 startP = new(0, Random.Shared.Int(0, wid));
        Vector2 endP = new(len, Random.Shared.Int(0, wid));
        List<Vector2> branchP = MakeBranches(len, wid, startP, endP);
        List<List<bool>> gridRoads = FillGridEmpty(len + 1, wid + 1);

        MarkRoad(gridRoads, startP, endP, len, wid, debug);

        if (debug) {
            for (int l = 0; l <= len; l++) for (int w = 0; w <= wid; w++) {
                DebugOverlay.Sphere(new Vector3(l * 512, w * 512, 0), 60, gridRoads[l][w] ? Color.Green : Color.Red, 16, false);
            }
        }

        foreach (Vector2 curP in branchP) {
            Vector2 nearestP = new(len * 0.5f, wid * 0.5f);
            for (int i = 0; i < 50; i++) {
                int curX = (int)curP.x;
                int curY = (int)curP.y;
                int dirX = (int)Math.Clamp(curP.x + i * 0.8f, 0, len);
                int dirY = (int)Math.Clamp(curP.y + i * 0.8f, 0, wid);
                int negX = (int)Math.Clamp(curP.x - i * 0.8f, 0, len);
                int negY = (int)Math.Clamp(curP.y - i * 0.8f, 0, wid);

                if (gridRoads[dirX][curY]) nearestP = new(dirX, curY); // up
                if (gridRoads[dirX][dirY]) nearestP = new(dirX, dirY); // up left
                if (gridRoads[curX][dirY]) nearestP = new(curX, dirY); // left
                if (gridRoads[negX][dirY]) nearestP = new(negX, dirY); // down left
                if (gridRoads[negX][curY]) nearestP = new(negX, curY); // down
                if (gridRoads[negX][negY]) nearestP = new(negX, negY); // down right
                if (gridRoads[curX][negY]) nearestP = new(curX, negY); // right
                if (gridRoads[dirX][negY]) nearestP = new(dirX, negY); // up right

                if (nearestP != new Vector2(len * 0.5f, wid * 0.5f)) break;
            }

            MarkRoad(gridRoads, curP, nearestP, len, wid, debug);
        }

        // *
        // * road stage
        // *
        GenerateRoads(lvl, gridRoads, len, wid);

        if (debug) {
            DebugOverlay.Sphere(startP * 512, 100, Color.White, 16, false);
            DebugOverlay.Sphere(endP * 512, 100, Color.White, 16, false);
        }

        // cleanup empty tiles
        foreach (Entity ent in Entity.All) {
            if (ent is TileEmpty) ent.Delete();
        }

        await GameTask.DelayRealtime(100);

        // *
        // * walls stage
        // *
        for (int l = 0; l <= len; l++) for (int w = 0; w <= wid; w++) {
            lvl.tiles[l][w].MakeWalls(lvl.wallType);
        }

        // *
        // * events stage
        // *
        new TileEventStart().Init(lvl.tiles[(int)startP.x][(int)startP.y]);
        lvl.startPos = lvl.tiles[(int)startP.x][(int)startP.y].Transform;
        new TileEventEnd().Init(lvl.tiles[(int)endP.x][(int)endP.y]);
        lvl.endPos = lvl.tiles[(int)endP.x][(int)endP.y].Transform;

        for (int l = 0; l <= len; l++) for (int w = 0; w <= wid; w++) {
            if (lvl.tiles[l][w] is TileEmpty) continue;
            Vector2 e = new(l, w);
            if (e == startP || e == endP) continue;

            if (lvl.tiles[l][w] is TileEnd || Random.Shared.Float(0, 1) > 0.99f) {
                new TileEventPowerups().Init(lvl.tiles[l][w]);
                continue;
            }

            if (Random.Shared.Float(0, 1) > 0.85f) {
                new TileEventFight().Init(lvl.tiles[l][w]);
                continue;
            }
        }

        // *
        // * props stage
        // *
        // for (int l = 0; l < length; l++) for (int w = 0; w < width; w++) {
        // }
        

        GGame.Cur.currentWorld = lvl;
        return;
    }

    private static void MarkRoad(List<List<bool>> grid, Vector2 startP, Vector2 endP, int len, int wid, bool debug = false) {
        bool making = true;
        int i = 0, lastDetour = 1;
        Vector2 curP = startP;
        Vector3 dir = new((endP - curP).Normal, 0);

        grid[(int)startP.x][(int)startP.y] = true;
        grid[(int)endP.x][(int)endP.y] = true;

        while (making) {
            i++; lastDetour++;
            
            // detour
            if (lastDetour > 6 && Random.Shared.Float(0, 1) > 0.5f) {
                if (Vector2.Distance(curP, endP) < 2) continue;
                
                dir *= Rotation.FromYaw(Random.Shared.Float(-80, 80));
                lastDetour = 0;
            } else if (lastDetour > 2) {
                dir = new((endP - curP).Normal, 0);
            }
            
            Vector2 newP = curP + MakeVec2(dir);
            newP.x = newP.x.Clamp(0, len);
            newP.y = newP.y.Clamp(0, wid);

            int xOffset = (int)newP.x - (int)curP.x;
            int yOffset = (int)newP.y - (int)curP.y;

            if (xOffset != 0 && yOffset != 0) {
                grid[(int)curP.x + xOffset][(int)curP.y] = true;
            }

            grid[(int)newP.x][(int)newP.y] = true;

            if (debug) {
                DebugOverlay.Line(curP * 512, newP * 512, Color.White, 16, false);
                DebugOverlay.Line(FloorVec2(curP) * 512, FloorVec2(newP) * 512, Color.Blue, 16, false);
            }

            curP = newP;
            if (i > 100 || FloorVec2(curP) == endP) {
                making = false;
            }
        }
    }

    private static void GenerateRoads(World lvl, List<List<bool>> gridRoads, int len, int wid) {
        for (int l = 0; l <= len; l++) for (int w = 0; w <= wid; w++) {
            if (gridRoads[l][w]) {
                bool[] dir = {false, false, false, false};
                int connected = 0;

                if (l + 1 <= len && gridRoads[l + 1][w]) dir[0] = true;
                if (w + 1 <= wid && gridRoads[l][w + 1]) dir[1] = true;
                if (l - 1 >= 0   && gridRoads[l - 1][w]) dir[2] = true;
                if (w - 1 >= 0   && gridRoads[l][w - 1]) dir[3] = true;
                foreach (bool e in dir) if (e) connected += 1;

                // ignore rotation numbers, the model vs them doesnt make much sense
                switch (connected) {
                    case 1: {
                        for (int i = 0; i < 4; i++) {
                            if (dir[i]) {
                                Tile(lvl, TileType.Endcap, new Vector2(l, w), i);
                                break;
                            }
                        }
                        break;
                    }
                    case 2: {
                        if (dir[0] && dir[2] || dir[1] && dir[3]) {
                            Tile(lvl, TileType.Straight, new Vector2(l, w), dir[0] ? 0 : 1);
                        } else {
                            if (dir[0]) {
                                Tile(lvl, TileType.Corner, new Vector2(l, w), dir[1] ? 1 : 0);
                            } else {
                                Tile(lvl, TileType.Corner, new Vector2(l, w), dir[3] ? 3 : 2);
                            }
                        }
                        break;
                    } 
                    case 3: {
                        if (dir[0] && dir[2]) {
                            Tile(lvl, TileType.T, new Vector2(l, w), dir[1] ? 2 : 0);
                        } else {
                            Tile(lvl, TileType.T, new Vector2(l, w), dir[2] ? 3 : 1);
                        }
                        break;
                    } 
                    case 4: {
                        Tile(lvl, TileType.X, new Vector2(l, w));
                        break;
                    } 
                }
            }
        }
    }

    private static Vector2 MakeVec2(Vector3 vec) {
        return new Vector2(vec.x, vec.y);
    }
    private static Vector2 FloorVec2(Vector3 vec) {
        return new Vector2((int)vec.x, (int)vec.y);
    }

    private static void Tile(World lvl, TileType type, Vector2 pos, int rot = 0, Color? color = null) {
        var (x, y) = ((int)pos.x, (int)pos.y);
        if (lvl.tiles[x][y] is not TileEmpty) return;

        switch (type) {
            case TileType.Endcap: {
                lvl.tiles[x][y] = new TileEnd(new Vector2(x, y) * 512, rot) {
                    RenderColor = color ?? Color.White,
                };
                break;
            }
            case TileType.Straight: {
                lvl.tiles[x][y] = new TileStraight(new Vector2(x, y) * 512, rot) {
                    RenderColor = color ?? Color.White,
                };
                break;
            }
            case TileType.Corner: {
                lvl.tiles[x][y] = new TileCorner(new Vector2(x, y) * 512, rot) {
                    RenderColor = color ?? Color.White,
                };
                break;
            }
            case TileType.T: {
                lvl.tiles[x][y] = new TileT(new Vector2(x, y) * 512, rot) {
                    RenderColor = color ?? Color.White,
                };
                break;
            }
            case TileType.X: {
                lvl.tiles[x][y] = new TileX(new Vector2(x, y) * 512, 0) {
                    RenderColor = color ?? Color.White,
                };
                break;
            }
        }
    }

    private static List<Vector2> MakeBranches(int l, int w, Vector2 startP, Vector2 endP) {
        List<Vector2> branches = new();
        for (int i = 0; i < ((byte)Random.Shared.Int(5, 8)); i++) {
            Vector2 pos = new(Random.Shared.Int(0, l), Random.Shared.Int(0, w));
    
            while (pos == startP || pos == endP) {
                pos = new(Random.Shared.Int(0, l), Random.Shared.Int(0, w));
            }

            branches.Add(pos);
        }
        return branches;
    }

    private static List<List<Tile>> FillTilesEmpty(int l, int w) {
        List<List<Tile>> tiles = new();
        for (int len = 0; len < l; len++) {
            tiles.Add(new List<Tile>());

            for (int wid = 0; wid < w; wid++) {
                tiles[len].Add(new TileEmpty());
            }
        }
        return tiles;
    }

    private static List<List<bool>> FillGridEmpty(int l, int w) {
        List<List<bool>> grid = new();
        for (int len = 0; len < l; len++) {
            grid.Add(new List<bool>());

            for (int wid = 0; wid < w; wid++) {
                grid[len].Add(false);
            }
        }
        return grid;
    }
}
