

Merge gems if a lot of gems are in the same spatial bucket


Harvester strategy, target closest gems
Target Cluster seems buggy, goes top left(0,0?) too much
Ability booster like multi cast: gives a chance when an ability triggers to reset cooldown on another one.




Unhandled exception. Microsoft.Xna.Framework.Audio.InstancePlayLimitException (0x80004005): External component has thrown an exception.
   at Microsoft.Xna.Framework.Audio.OpenALSoundController.ReserveSource()
   at Microsoft.Xna.Framework.Audio.SoundEffectInstance.PlatformPlay()
   at Microsoft.Xna.Framework.Audio.SoundEffectInstance.Play()
   at Microsoft.Xna.Framework.Audio.SoundEffect.Play(Single volume, Single pitch, Single pan)
   at AudioManager.PlaySound(SoundEffect soundEffect, Single pitch, Single pan) in /home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/AudioManager.cs:line 161
   at UntitledGemGame.Systems.HarvesterCollectionSystem.CollectGem(Gem gem, Harvester harvester) in /home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/Systems/HarvesterSystem.cs:line 464
   at UntitledGemGame.Systems.HarvesterCollectionSystem.Update(GameTime gameTime) in /home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/Systems/HarvesterSystem.cs:line 669
   at MonoGame.Extended.ECS.World.Update(GameTime gameTime)
   at UntitledGemGame.Screens.UntitledGemGameGameScreen.Update(GameTime gameTime) in /home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/Screens/GameScreen.cs:line 557
   at MonoGame.Extended.Screens.ScreenManager.Update(GameTime gameTime)
   at Microsoft.Xna.Framework.Game.SortingFilteringCollection`1.ForEachFilteredItem[TUserData](Action`2 action, TUserData userData)
   at JapeFramework.BaseGame.Update(GameTime gameTime) in /home/johan/Dev/workspaces/MonogameBase/JapeFramework/BaseGame.cs:line 402
   at UntitledGemGame.GameMain.Update(GameTime gameTime) in /home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/GameMain.cs:line 645
   at Microsoft.Xna.Framework.Game.DoUpdate(GameTime gameTime)
   at Microsoft.Xna.Framework.Game.Tick()
   at Microsoft.Xna.Framework.SdlGamePlatform.RunLoop()
   at UntitledGemGame.Program.Main(String[] args) in /home/johan/Dev/workspaces/MonogameBase/Games/UntitledGemGame/Program.cs:line 31
   at UntitledGemGame.Program.<Main>(String[] args)
