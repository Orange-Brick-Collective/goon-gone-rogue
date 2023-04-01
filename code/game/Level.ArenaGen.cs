using Sandbox;
using System;
using System.Collections.Generic;

namespace GGame;

public class ArenaGen {
    public static ArenaGen Cur {get; set;}

    public ArenaGen() {
        if (Cur is not null) return;
        Cur = this;
    }

    [ConCmd.Server("gen_arena")]
    public static async void GenerateLevelCMD() {
        await Cur.GenerateLevel();
    }

    public async System.Threading.Tasks.Task GenerateLevel(int? wallType = null) {
        Game.AssertServer();
        Log.Info("Generating Arena");

        foreach (Entity ent in Entity.All) {
            if (ent.Tags.Has("generatedarena")) ent.Delete();
        }

        Arena lvl = new() {
            wallType = wallType ?? GGame.Cur.currentWorld?.wallType ?? 0,
        };
        string[] models = WallModels.GetModels(wallType ?? lvl.wallType);

        Transform pos = GGame.Cur.ArenaMarker;

        for (int i = 0; i < 4; i++) {
            int dirX = 0, dirY = 0;

            if (i == 0) dirX = 752;
            else if (i == 2) dirX = -752;
            
            if (i == 1) dirY = 752;
            else if (i == 3) dirY = -752;

            for (int j = 0; j < 3; j++) {
                Vector3 arenaPos;
                if (i == 0 || i == 2) {
                    arenaPos = new Vector3(dirX, (j * 512) - 512, 0);
                } else {
                    arenaPos = new Vector3((j * 512) - 512, dirY, 0);
                }

                ModelEntity ent = new() {
                    Position = pos.Position + arenaPos,
                    Rotation = Rotation.FromYaw((i + 2) * 90),
                };
                ent.Tags.Add("generatedarena");
                ent.SetModel(models[Random.Shared.Int(0, models.Length - 1)]);
            }
        }

        GGame.Cur.currentArena = lvl;
        await GameTask.DelayRealtime(1);
        return;
    }
}