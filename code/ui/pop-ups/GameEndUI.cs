using System;
using Sandbox;
using Sandbox.UI;

namespace GGame;

public class GameEndUI : Panel {
    public Panel left, right, buttons;

    public GameEndUI() {
        StyleSheet.Load("ui/pop-ups/GameEndUI.scss");

        Panel ui = new(this) {Classes = "content"};

        ui.AddChild(new Label() {Classes = "title", Text = "Game Over"});
        ui.AddChild(new Label() {Classes = "description", Text = "-- Goons Gone Rogue --" +
        "How you did this run..." +
        $"Depth: {GGame.Current.CurrentDepth}" +
        $"Score: {GGame.Current.Score}" +
        $"Kills: {GGame.Current.Kills}" +
        $"Damage Dealt: {GGame.Current.DamageDealt}" +
        $"Damage Taken: {GGame.Current.DamageTaken}"
        });

        ui.AddChild(new Button("Return to menu", "", BackToMenu));
    }

    private void BackToMenu() {
        ServerBackToMenu("1209825");
        Delete();
    }
    [ConCmd.Server]
    private static void ServerBackToMenu(string password) {
        if (password != "1209825") return;

        Player.Current.InMenu = true;
        GGame.Current.FromBlackUI();
    }
}