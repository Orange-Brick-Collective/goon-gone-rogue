﻿using Sandbox;
using System;

namespace GGame;

public partial class Player : Pawn {
	public static Player Cur {get; set;}

	[Net, Change] public bool InMenu {get; set;} = true;

	[ClientInput] public Vector3 InputDirection {get; protected set;}
	[ClientInput] public Angles ViewAngles {get; set;}

	public override void BuildInput() {
		InputDirection = Input.AnalogMove;

		Angles look = Input.AnalogLook;

		if (!InMenu) {
			Angles viewAngles = ViewAngles;
			viewAngles += look;
			viewAngles.pitch = viewAngles.pitch.Clamp(-72, 68);
			ViewAngles = viewAngles.Normal;
		} else {
			ViewAngles = new Angles(20, Time.Tick * 0.16f, 0);
		}
	}

	public override void Spawn() {
		base.Spawn();
		Cur = this;

		Tags.Add("player");
		Tags.Add("team0");
		Name = "Player";
		
		EnableTouch = true;
		EnableDrawing = true;

		SetModel("models/player2.vmdl");
		SetupPhysicsFromAABB(PhysicsMotionType.Keyframed, new Vector3(-16, -16, 0), new Vector3(16, 16, 70));
        
		weapon = new();
        weapon.Init("models/gun.vmdl");
        weapon.Position = Position + new Vector3(0, -12 * Scale, 35 * Scale);
        weapon.Rotation = Rotation;
        weapon.Owner = this;
        weapon.Parent = this;

		MaxHealth = 200;
		Health = 200;
		BaseWeaponDamage = 10;
	}
	public override void ClientSpawn() {
		Cur = this;
		base.ClientSpawn();
	}

	public override void StartTouch(Entity ent) {
		if (!Game.IsServer) return;

		switch (ent) {
			case TileEventFight: {
				ent.Delete();
				GGame.Cur.TransitionStartFight();
				break;
			}
			case TileEventEnd: {
				GGame.Cur.TransitionLevel();
				break;
			}
		}
	}

	public override void OnKilled() {
		// game over

		// back to menu
	}

	public void OnInMenuChanged() {
		if (InMenu) {
			Hud._hud.RootPanel.AddChild(new Menu());
		} else{
			foreach (Menu m in Hud._hud.RootPanel.ChildrenOfType<Menu>()) {
				m.Delete();
			}
		}
	}

	// *
	// * SIMULATES
	// *

	public override void Simulate(IClient cl) {
		base.Simulate(cl);
		if (InMenu) return;

		SimulateMovement();
		SimulateUse();

		if (Input.Down(InputButton.PrimaryAttack) && Game.IsServer) {
			FireGun();
		}

		if (Input.Pressed(InputButton.Slot1) && Game.IsServer) {
			TraceResult tr = Trace.Ray(Camera.Position, Camera.Position + Camera.Rotation.Forward * 4000).Ignore(this).Run();
			Goon g = new();
			g.Init(0, this);
			g.Position = tr.EndPosition + Vector3.Up * 5;
		}

		if (Input.Pressed(InputButton.Slot2) && Game.IsServer) {
			TraceResult tr = Trace.Ray(Camera.Position, Camera.Position + Camera.Rotation.Forward * 4000).Ignore(this).Run();
			Goon g = new();
			g.Init(1);
			g.Position = tr.EndPosition + Vector3.Up * 5;

			float rHP = Random.Shared.Float(50, 400);
			g.MaxHealth = rHP;
			g.Health = rHP;

			float rScale = Random.Shared.Float(0.4f, 2f);
			g.Scale = rScale;
		}

		if (Input.Pressed(InputButton.Reload) && Game.IsServer) {
			TraceResult tr = Trace.Ray(Camera.Position, Camera.Position + Camera.Rotation.Forward * 400).Ignore(this).Run();
			PowerupEntity p = new();
			p.Init(Powerups.GetRandomIndex);
			p.Position = tr.EndPosition + Vector3.Up * 50;
		}

		if (Input.Pressed(InputButton.Slot4) && Game.IsServer) {
			TraceResult tr = Trace.Ray(Camera.Position, Camera.Position + Camera.Rotation.Forward * 8000).Ignore(this).Run();
			Position = tr.EndPosition + tr.Normal * 50;
		}
	}

	public override void FrameSimulate(IClient cl) {
		base.FrameSimulate( cl );
		SimulateCamera();
	}

	public void SimulateCamera() {
		Vector3 pos = Position;
		if (!InMenu) {
			pos.z += 60;
			pos += Rotation.Right * 25;
			TraceResult tr = Trace.Ray(pos, pos - (ViewAngles.Forward * 60)).WithoutTags("player", "goon", "trigger").Run();
			pos = tr.EndPosition - tr.Direction * 15;
		} else {
			pos.z += 40;
			pos -= ViewAngles.Forward * 100;
		}
		
		Camera.Position = pos;
		Camera.Rotation = ViewAngles.ToRotation();

		Camera.FieldOfView = Screen.CreateVerticalFieldOfView(Game.Preferences.FieldOfView);
	}
}
