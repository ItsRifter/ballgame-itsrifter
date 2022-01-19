﻿
using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Ballers
{
	public partial class Ball : Player
	{
		public static List<Ball> ReplayGhosts = new();

		[Net, Predicted] public int CheckpointIndex { get; private set; } = 0;
		[Net, Predicted] public bool Popped { get; private set; }

		public override void Respawn()
		{
			if ( !(this as ModelEntity).IsValid() )
				return;

			Host.AssertServer();

			Velocity = Vector3.Zero;

			Popped = false;
			ActiveTick = 0;
			EnableDrawing = true;

			SetModel( "models/ball.vmdl" );

			//Controller = Client.IsValid() ? ControlType.Player : ControlType.Replay;
			if ( Controller == ControlType.Player )
			{
				ReplayData = new ReplayData();
				Camera = new BallCamera();
			}
			else
			{
				if ( !ReplayGhosts.Contains( this ) )
					ReplayGhosts.Add( this );
			}

			if ( Client.IsValid() )
				ClothingData = Client.GetClientData( "avatar" );

			PhysicsEnabled = false;

			// for water collision effects!
			SetupPhysicsFromSphere( PhysicsMotionType.Keyframed, Vector3.Zero, 40f );
			EnableAllCollisions = false;
			EnableTraceAndQueries = true;
			ClearCollisionLayers();
			SetInteractsWith( CollisionLayer.Water );

			EnableShadowCasting = true;
			Transmit = TransmitType.Always;

			SetSpawnpoint();

			ResetInterpolation();

			RespawnRpc();
		}

		[ClientRpc]
		private void RespawnRpc()
		{
			SetupColors();
		}

		private void SetSpawnpoint()
		{
			Position = Vector3.Up * 40f;

			var spawnpoints = All.OfType<BallSpawn>();
			var desiredSpawn = spawnpoints.Where( s => s.Index == CheckpointIndex ).FirstOrDefault();
			if ( desiredSpawn != null )
			{
				Position += desiredSpawn.Position;
				return;
			}

			var spawnpoint = All.OfType<SpawnPoint>().FirstOrDefault();
			if ( spawnpoint != null )
				Position += spawnpoint.Position;
		}

		public override void ClientSpawn()
		{
			base.ClientSpawn();

			SetupTerry();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();

			if ( Terry.IsValid() )
				Terry.Delete();

			if ( ReplayGhosts.Contains( this ) )
				ReplayGhosts.Remove( this );
		}

		public async void RespawnAsync( float time )
		{
			await GameTask.DelaySeconds( time );
			Respawn();
		}


		private void PredictedSound( string soundName )
		{
			PlaySound( soundName );

			if ( IsServer )
				PredictedSoundRpc( soundName );
		}

		[ClientRpc]
		public void PredictedSoundRpc( string soundName )
		{
			if ( Client != Local.Client || Controller == ControlType.Replay )
				PredictedSound( soundName );
		}


		public void Pop( bool predicted = true )
		{
			if ( (IsServer || !predicted) && Popped )
				return;

			if ( IsServer )
			{
				PopRpc( predicted );
				RespawnAsync( 2f );
				Client.AddInt( "deaths" );
			}
			else
			{
				Ragdoll();
				BallDome.Create( this );
			}

			Popped = true;
			EnableDrawing = false;
		}

		[ClientRpc]
		public void PopRpc( bool predicted = true )
		{
			if ( !predicted || Client != Local.Client || Controller == ControlType.Replay )
				Pop();
		}

		private bool isColored = false;
		private float GetHue()
		{
			int id = Rand.Int( 65535 );

			if ( Client.IsValid() )
				id = (int)(Client.PlayerId & 65535);

			Random seedColor = new Random( id );
			return (float)seedColor.NextDouble() * 360f;
		}

		private void SetupColors()
		{
			float hue = GetHue();

			float saturation = Controller == ControlType.Player ? 0.8f : 0.35f;

			Color ballColor = new ColorHsv( hue, saturation, 1f );
			Color ballColor2 = new ColorHsv( (hue + 25f) % 360, saturation, 1f );

			SceneObject.SetValue( "tint", ballColor );
			SceneObject.SetValue( "tint2", ballColor2 );

			isColored = true;
		}

		[Event.Frame]
		public void Frame()
		{
			UpdateTerry();

			if ( !SceneObject.IsValid() )
				return;

			if ( !isColored )
				SetupColors();
		}

		[ServerCmd( "kill" )]
		public static void Kill()
		{
			if ( ConsoleSystem.Caller != null && ConsoleSystem.Caller.Pawn is Ball player )
				player.Pop( false );
		}

		[ServerCmd( "reset" )]
		public static void Reset()
		{
			if ( ConsoleSystem.Caller != null && ConsoleSystem.Caller.Pawn is Ball player )
			{
				player.CheckpointIndex = 0;
				player.Pop( false );
			}
		}
	}
}
