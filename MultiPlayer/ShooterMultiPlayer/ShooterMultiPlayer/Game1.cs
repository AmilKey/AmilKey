using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Net;

namespace ShooterMultiPlayer
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class MultiGame : Microsoft.Xna.Framework.Game
    {
        #region vars

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Player LocalPlayer;
        Player RemotePlayer;

        // Keyboard states used to determine key presses
        KeyboardState currentKeyboardState;
        KeyboardState previousKeyboardState;

        // Gamepad states used to determine button presses
        GamePadState currentGamePadState;
        GamePadState previousGamePadState;

        // A movement speed for the player
        float playerMoveSpeed;

        // A random number generator
        Random random;

        Texture2D projectileTexture;
        List<Projectile> projectiles;

        // The rate of fire of the player laser
        TimeSpan fireTime;
        TimeSpan previousFireTime;

        // Image used to display the static background
        Texture2D mainBackground;

        // Parallaxing Layers
        ParallaxingBackground bgLayer1;
        ParallaxingBackground bgLayer2;

        // The font used to display UI elements
        SpriteFont font;

        //Network

        // Represents different states of the game
        public enum GameState
        {
            SignIn, FindSession,
            CreateSession, Start, InGame, GameOver
        }

        // Represents different types of network messages
        public enum MessageType
        {
            StartGame, EndGame, RestartGame,
            RejoinLobby, UpdatePlayerPos
        }

        // Current game state
        GameState currentGameState = GameState.SignIn;

        NetworkSession networkSession;
        AvailableNetworkSessionCollection availableSessions;
        int selectedSessionIndex;
        PacketReader packetReader = new PacketReader();
        PacketWriter packetWriter = new PacketWriter();

        #endregion 


        public MultiGame()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            // Add Gamer Services
            Components.Add(new GamerServicesComponent(this));

            // Respond to the SignedInGamer event
    //        SignedInGamer.SignedIn +=
       //         new EventHandler<SignedInEventArgs>(SignedInGamer_SignedIn);
        }

       
        void SignedInGamer_SignedIn(object sender, SignedInEventArgs e)
        {
            e.Gamer.Tag = new Player();
        }

        private void HandleGameplayInput(Player player, GameTime gameTime)
        {

            // change UpdateInput to take a Player
            UpdatePlayer(gameTime);

          //  player.Update(gameTime);

            networkSession.Update();

            //base.Update(gameTime);
        }


        private void DrawGameplay(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            Player player;
            if (networkSession != null)
            {

                foreach (NetworkGamer networkGamer in networkSession.AllGamers)
                {
                    player = networkGamer.Tag as Player;
                    if (networkGamer.IsLocal)
                    {
                        LocalPlayer.Draw(spriteBatch);
                    }
                    else
                    {
                        RemotePlayer.Draw(spriteBatch);
                    }
                }
            }
        }

        private void DrawTitleScreen()
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            string message = "";

            if (SignedInGamer.SignedInGamers.Count == 0)
            {
                message = "No profile signed in!  \n" +
                    "Press the Home key on the keyboard or \n" +
                    "the Xbox Guide Button on the controller to sign in.";
            }
            else
            {
                message += "Press A to create a new session\n" +
                    "X to search for sessions\nB to quit\n\n";
            }
            spriteBatch.Begin();
            spriteBatch.DrawString(font, message,
                new Vector2(101, 101), Color.Black);
            spriteBatch.DrawString(font, message,
                new Vector2(100, 100), Color.White);
            spriteBatch.End();
        }

        protected void HandleTitleScreenInput()
        {
            if (currentKeyboardState.IsKeyDown(Keys.A))
            {
                CreateSession();
            }
            else if (currentKeyboardState.IsKeyDown(Keys.X))
            {
                availableSessions = NetworkSession.Find(
                    NetworkSessionType.SystemLink, 1, null);

                selectedSessionIndex = 0;
            }
            else if (currentKeyboardState.IsKeyDown(Keys.B))
            {
                Exit();
            }
        }

        void CreateSession()
        {
            networkSession = NetworkSession.Create(
                NetworkSessionType.SystemLink,
                1, 2, 2,
                null);

            networkSession.AllowHostMigration = true;
            networkSession.AllowJoinInProgress = true;

            HookSessionEvents();
        }

        private void HookSessionEvents()
        {
            networkSession.GamerJoined +=
                new EventHandler<GamerJoinedEventArgs>(
                    networkSession_GamerJoined);
        }

        void networkSession_GamerJoined(object sender, GamerJoinedEventArgs e)
        {
            if (!e.Gamer.IsLocal)
            {
                e.Gamer.Tag = new Player();
            }
            else
            {
                e.Gamer.Tag = GetPlayer(e.Gamer.Gamertag);
            }
        }

        Player GetPlayer(String gamertag)
        {
            foreach (SignedInGamer signedInGamer in
                SignedInGamer.SignedInGamers)
            {
                if (signedInGamer.Gamertag == gamertag)
                {
                    return signedInGamer.Tag as Player;
                }
            }

            return new Player();
        }


        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            LocalPlayer = new Player();
            RemotePlayer = new Player();

            // Set a constant player move speed
            playerMoveSpeed = 8.0f;

            //Enable the FreeDrag gesture.
            TouchPanel.EnabledGestures = GestureType.FreeDrag;

            // Initialize our random number generator
            random = new Random();

            projectiles = new List<Projectile>();

            // Set the laser to fire every quarter second
            fireTime = TimeSpan.FromSeconds(.25f);

          bgLayer1 = new ParallaxingBackground();
           bgLayer2 = new ParallaxingBackground();

            //network
           

           base.Initialize();

           Components.Add(new GamerServicesComponent(this));
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load the player resources
            Animation LocalPlayerAnimation = new Animation();
            Animation RemotePlayerAnimation = new Animation();
            Texture2D playerTexture = Content.Load<Texture2D>("shipAnimation");
            Texture2D RemotePlayerTexture = Content.Load<Texture2D>("RemoteshipAnimation");

            RemotePlayerAnimation.Initialize(RemotePlayerTexture, Vector2.Zero, 115, 69, 8, 30, Color.White, 1f, true);
            LocalPlayerAnimation.Initialize(playerTexture, Vector2.Zero, 115, 69, 8, 30, Color.White, 1f, true);

            Vector2 LocalPlayerPosition = new Vector2(GraphicsDevice.Viewport.TitleSafeArea.X+LocalPlayerAnimation.FrameWidth/3, GraphicsDevice.Viewport.TitleSafeArea.Y
            + GraphicsDevice.Viewport.TitleSafeArea.Height / 2);
            LocalPlayer.Initialize(LocalPlayerAnimation, LocalPlayerPosition);

            Vector2 RemotePlayerPosition = new Vector2(
               GraphicsDevice.Viewport.TitleSafeArea.X + GraphicsDevice.Viewport.TitleSafeArea.Width - RemotePlayerAnimation.FrameWidth/3,
               GraphicsDevice.Viewport.TitleSafeArea.Y + GraphicsDevice.Viewport.TitleSafeArea.Height / 2);
            RemotePlayer.Initialize(RemotePlayerAnimation, RemotePlayerPosition);

            projectileTexture = Content.Load<Texture2D>("laser");
            // TODO: use this.Content to load your game content here

            // Load the parallaxing background
            bgLayer1.Initialize(Content, "bgLayer1", GraphicsDevice.Viewport.Width, -1);
            bgLayer2.Initialize(Content, "bgLayer2", GraphicsDevice.Viewport.Width, -2);

            // Load the score font
            font = Content.Load<SpriteFont>("gameFont");

            mainBackground = Content.Load<Texture2D>("mainbackground");
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            //// Allows the game to exit
            //if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            //    this.Exit();
            //// Save the previous state of the keyboard and game pad so we can determinesingle key/button presses
            //previousGamePadState = currentGamePadState;
            //previousKeyboardState = currentKeyboardState;

            //// Read the current state of the keyboard and gamepad and store it
            //currentKeyboardState = Keyboard.GetState();
            //currentGamePadState = GamePad.GetState(PlayerIndex.One);

            //UpdatePlayer(gameTime);

            //RemotePlayer.Update(gameTime);
            //// Update the collision
            //UpdateCollision();

            //// Update the projectiles
            //UpdateProjectiles();

            // Only run the Update code if the game is currently active.
            // This prevents the game from progressing while
            // gamer services windows are open.
            if (this.IsActive)
            {
                // Run different methods based on game state
                switch (currentGameState)
                {
                    case GameState.SignIn:
                        Update_SignIn();
                        break;
                    case GameState.FindSession:
                        Update_FindSession();
                        break;
                    case GameState.CreateSession:
                        Update_CreateSession();
                        break;
                    case GameState.Start:
                        Update_Start(gameTime);
                        break;
                    case GameState.InGame:
                        Update_InGame(gameTime);
                        break;
                    case GameState.GameOver:
                        Update_GameOver(gameTime);
                        break;
                }
            }
            // Update the network session and pump network messages
            if (networkSession != null)
                networkSession.Update();


            // Update the parallaxing background
          bgLayer1.Update();
        bgLayer2.Update();

            base.Update(gameTime);
        }

        protected void Update_SignIn()
        {
            // If no local gamers are signed in, show sign-in screen
            if (Gamer.SignedInGamers.Count < 1)
            {
                Guide.ShowSignIn(1, false);
            }
            else
            {
                // Local gamer signed in, move to find sessions
                currentGameState = GameState.FindSession;
            }
        }

        private void Update_FindSession()
        {
            // Find sessions of the current game
            AvailableNetworkSessionCollection sessions =
                NetworkSession.Find(NetworkSessionType.SystemLink, 1, null);
            if (sessions.Count == 0)
            {
                // If no sessions exist, move to the CreateSession game state
                currentGameState = GameState.CreateSession;
            }
            else
            {
                // If a session does exist, join it, wire up events,
                // and move to the Start game state
                networkSession = NetworkSession.Join(sessions[0]);
                WireUpEvents();
                currentGameState = GameState.Start;
            }
        }

        protected void WireUpEvents()
        {
            // Wire up events for gamers joining and leaving
            networkSession.GamerJoined += GamerJoined;
            networkSession.GamerLeft += GamerLeft;
        }

        void GamerJoined(object sender, GamerJoinedEventArgs e)
        {
            // Gamer joined. Set the tag for the gamer to a new UserControlledSprite.
            // If the gamer is the host, create a chaser; if not, create a chased.
            if (e.Gamer.IsHost)
            {
                e.Gamer.Tag = LocalPlayer;
            }
            else
            {
                e.Gamer.Tag = RemotePlayer;
            }
        }

      
        void GamerLeft(object sender, GamerLeftEventArgs e)
        {
              // Dispose of the network session, set it to null.
               // Stop the soundtrack and go
              // back to searching for sessions.
             networkSession.Dispose(  );
             networkSession = null;
           //  trackInstance.Stop(  );
             currentGameState = GameState.FindSession;
        }

        private void Update_CreateSession()
        {
            // Create a new session using SystemLink with a max of 1 local player
            // and a max of 2 total players
            networkSession = NetworkSession.Create(NetworkSessionType.SystemLink, 1, 2);
            networkSession.AllowHostMigration = true;
            networkSession.AllowJoinInProgress = true;
            // Wire up events and move to the Start game state
            WireUpEvents();
            currentGameState = GameState.Start;
        }
        private void Update_Start(GameTime gameTime)
        {
            // Get local gamer
            LocalNetworkGamer localGamer = networkSession.LocalGamers[0];
            // Check for game start key or button press
            // only if there are two players
            if (networkSession.AllGamers.Count == 2)
            {
                // If space bar or Start button is pressed, begin the game
                if (Keyboard.GetState().IsKeyDown(Keys.Space) ||
                    GamePad.GetState(PlayerIndex.One).Buttons.Start ==
                    ButtonState.Pressed)
                {

                    // Send message to other player that we're starting
                    packetWriter.Write((int)MessageType.StartGame);
                    localGamer.SendData(packetWriter, SendDataOptions.Reliable);
                    // Call StartGame
                    StartGame();
                }
            }
            // Process any incoming packets
            ProcessIncomingData(gameTime);
        }

        protected void StartGame()
        {
            // Set game state to InGame
            currentGameState = GameState.InGame;
            // Start the soundtrack audio
            //SoundEffect se = Content.Load<SoundEffect>(@"audio\track");
            //trackInstance = se.CreateInstance();
            //trackInstance.IsLooped = true;
            //trackInstance.Play();
            //// Play the start sound
            //se = Content.Load<SoundEffect>(@"audio\start");
            //se.Play();
        }

        protected void ProcessIncomingData(GameTime gameTime)
        {
            // Process incoming data
            LocalNetworkGamer localGamer = networkSession.LocalGamers[0];
            // While there are packets to be read...
            while (localGamer.IsDataAvailable)
            {
                // Get the packet
                NetworkGamer sender;
                localGamer.ReceiveData(packetReader, out sender);
                // Ignore the packet if you sent it
                if (!sender.IsLocal)
                {
                    // Read messagetype from start of packet
                    // and call appropriate method
                    MessageType messageType = (MessageType)packetReader.ReadInt32();
                    switch (messageType)
                    {
                        case MessageType.EndGame:
                            EndGame();
                            break;
                        case MessageType.StartGame:
                            StartGame();
                            break;
                        case MessageType.RejoinLobby:
                            RejoinLobby();
                            break;
                        case MessageType.RestartGame:
                            RestartGame();
                            break;
                        case MessageType.UpdatePlayerPos:
                            UpdateRemotePlayer(gameTime);
                            break;
                    }
                }
            }
        }

        protected void EndGame()
        {
            // Play collision sound effect
            // (game ends when players collide)
            //SoundEffect se = Content.Load<SoundEffect>(@"audio\boom");
            //se.Play();
            //// Stop the soundtrack music
            //trackInstance.Stop();
            // Move to the game-over state
            currentGameState = GameState.GameOver;
        }

        private void RejoinLobby()
        {
            // Switch dynamite and gears sprites
            // as well as chaser versus chased
            SwitchPlayersAndReset(false);
            currentGameState = GameState.Start;
        }

        private void RestartGame()
        {
            // Switch dynamite and gears sprites
            // as well as chaser versus chased
            SwitchPlayersAndReset(true);
            StartGame();
        }

        private void SwitchPlayersAndReset(bool switchPlayers)
        {
            // Only do this if there are two players
            if (networkSession.AllGamers.Count == 2)
            {
                // Are we truly switching players or are we
                // setting the host as the chaser?
                if (switchPlayers)
                {
                    // Switch player sprites
                    if (((Player)networkSession.AllGamers[0].Tag).Active)
                    {
                        networkSession.AllGamers[0].Tag = LocalPlayer;
                        networkSession.AllGamers[1].Tag = RemotePlayer;
                    }
                    else
                    {
                        networkSession.AllGamers[0].Tag = RemotePlayer;
                        networkSession.AllGamers[1].Tag = LocalPlayer;
                    }
                }
                else
                {
                    // Switch player sprites
                    if (networkSession.AllGamers[0].IsHost)
                    {
                        networkSession.AllGamers[0].Tag = LocalPlayer;
                        networkSession.AllGamers[1].Tag = RemotePlayer;
                    }
                    else
                    {
                        networkSession.AllGamers[0].Tag = RemotePlayer;
                        networkSession.AllGamers[1].Tag = LocalPlayer;
                    }
                }
            }
        }


        protected void UpdateRemotePlayer(GameTime gameTime)
        {
            // Get the other (nonlocal) player
            NetworkGamer theOtherGuy = GetOtherPlayer();
            // Get the UserControlledSprite representing the other player
            Player theOtherSprite = ((Player)theOtherGuy.Tag);
            // Read in the new position of the other player
            Vector2 otherGuyPos = packetReader.ReadVector2();
            // If the sprite is being chased,
            // retrieve and set the score as well
            //if (!theOtherSprite.isChasing)
            //{
            //    int score = packetReader.ReadInt32();
            //    theOtherSprite.score = score;
            //}
            // Set the position
            theOtherSprite.Position = otherGuyPos;
            // Update only the frame of the other sprite
            // (no need to update position because you just did!)
            theOtherSprite.Update(gameTime);
        }
        protected NetworkGamer GetOtherPlayer()
        {
            // Search through the list of players and find the
            // one that's remote
            foreach (NetworkGamer gamer in networkSession.AllGamers)
            {
                if (!gamer.IsLocal)
                {
                    return gamer;
                }
            }
            return null;
        }

        private void Update_InGame(GameTime gameTime)
        {
            // Update the local player
            UpdateLocalPlayer(gameTime);
            // Read any incoming data
            ProcessIncomingData(gameTime);
            // Only host checks for collisions
            if (networkSession.IsHost)
            {
                // Only check for collisions if there are two players
                if (networkSession.AllGamers.Count == 2)
                {
                    Player sprite1 =
                        (Player)networkSession.AllGamers[0].Tag;
                    Player sprite2 =
                        (Player)networkSession.AllGamers[1].Tag;
                    UpdateCollision();
                    if (RemotePlayer.Health == 0)
                    {
                        // If the two players intersect, game over.
                        // Send a game-over message to the other player
                        // and call EndGame.
                        packetWriter.Write((int)MessageType.EndGame);
                        networkSession.LocalGamers[0].SendData(packetWriter,
                            SendDataOptions.Reliable);
                        EndGame();
                    }
                }
            }
        }

        protected void UpdateLocalPlayer(GameTime gameTime)
        {
            // Get local player
            LocalNetworkGamer localGamer = networkSession.LocalGamers[0];
            // Get the local player's sprite
            Player sprite = (Player)localGamer.Tag;
            // Call the sprite's Update method, which will process user input
            // for movement and update the animation frame
            UpdatePlayer(gameTime);
            // If this sprite is being chased, increment the score
            // (score is just the num milliseconds that the chased player
            // survived)
            //if (!sprite.Active)
            //    sprite.score += gameTime.ElapsedGameTime.Milliseconds;
            // Send message to other player with message tag and
            // new position of sprite
            packetWriter.Write((int)MessageType.UpdatePlayerPos);
            packetWriter.Write(sprite.Position);
            // If this player is being chased, add the score to the message
            //if (!sprite.isChasing)
            //    packetWriter.Write(sprite.score);
            // Send data to other player
            localGamer.SendData(packetWriter, SendDataOptions.InOrder);
        }

        private void Update_GameOver(GameTime gameTime)
        {
            KeyboardState keyboardState = Keyboard.GetState();
            GamePadState gamePadSate = GamePad.GetState(PlayerIndex.One);

            // If player presses Enter or A button, restart game
            if (keyboardState.IsKeyDown(Keys.Enter) ||
                gamePadSate.Buttons.A == ButtonState.Pressed)
            {
                // Send restart game message
                packetWriter.Write((int)MessageType.RestartGame);
                networkSession.LocalGamers[0].SendData(packetWriter,
                    SendDataOptions.Reliable);
                RestartGame();
            }
            // If player presses Escape or B button, rejoin lobby
            if (keyboardState.IsKeyDown(Keys.Escape) ||
                gamePadSate.Buttons.B == ButtonState.Pressed)
            {
                // Send rejoin lobby message
                packetWriter.Write((int)MessageType.RejoinLobby);
                networkSession.LocalGamers[0].SendData(packetWriter,
                    SendDataOptions.Reliable);
                RejoinLobby();
            }
            // Read any incoming messages
        }

        private void UpdatePlayer(GameTime gameTime)
        {
            LocalPlayer.Update(gameTime);

            // Windows Phone Controls
            while (TouchPanel.IsGestureAvailable)
            {
                GestureSample gesture = TouchPanel.ReadGesture();
                if (gesture.GestureType == GestureType.FreeDrag)
                {
                    LocalPlayer.Position += gesture.Delta;
                }
            }

            // Get Thumbstick Controls
            LocalPlayer.Position.X += currentGamePadState.ThumbSticks.Left.X * playerMoveSpeed;
            LocalPlayer.Position.Y -= currentGamePadState.ThumbSticks.Left.Y * playerMoveSpeed;

            // Use the Keyboard / Dpad
            if (currentKeyboardState.IsKeyDown(Keys.Left) ||
            currentGamePadState.DPad.Left == ButtonState.Pressed)
            {
                LocalPlayer.Position.X -= playerMoveSpeed;
                
            }
            if (currentKeyboardState.IsKeyDown(Keys.Right) ||
            currentGamePadState.DPad.Right == ButtonState.Pressed)
            {
                LocalPlayer.Position.X += playerMoveSpeed;
            }
            if (currentKeyboardState.IsKeyDown(Keys.Up) ||
            currentGamePadState.DPad.Up == ButtonState.Pressed)
            {
                LocalPlayer.Position.Y -= playerMoveSpeed;
            }
            if (currentKeyboardState.IsKeyDown(Keys.Down) ||
            currentGamePadState.DPad.Down == ButtonState.Pressed)
            {
                LocalPlayer.Position.Y += playerMoveSpeed;
            }


            // Make sure that the player does not go out of bounds
            LocalPlayer.Position.X = MathHelper.Clamp(LocalPlayer.Position.X, LocalPlayer.Width / 4, GraphicsDevice.Viewport.Width - LocalPlayer.Width / 3);
            LocalPlayer.Position.Y = MathHelper.Clamp(LocalPlayer.Position.Y, LocalPlayer.Health /4 , GraphicsDevice.Viewport.Height - LocalPlayer.Height / 3);

            // Fire only every interval we set as the fireTime
            //if (gameTime.TotalGameTime - previousFireTime > fireTime)
            //{
            //    // Reset our current time
            //    previousFireTime = gameTime.TotalGameTime;

            //    // Add the projectile, but add it to the front and center of the player
            //    AddProjectile(player.Position + new Vector2(player.Width / 2, 0));

            //    // Play the laser sound
            //    laserSound.Play();
            //}

            //Fire on press Space
            if (currentKeyboardState.IsKeyDown(Keys.Space) && (gameTime.TotalGameTime - previousFireTime > fireTime))
            {
                // Reset our current time
                previousFireTime = gameTime.TotalGameTime;

                // Add the projectile, but add it to the front and center of the player
                AddProjectile(LocalPlayer.Position + new Vector2(LocalPlayer.Width / 2, 0));

                // Play the laser sound
               // laserSound.Play();
            }

            // if player health goes to zero
            if (LocalPlayer.Health <= 0)
            {
                LocalPlayer.Health = 100;
            }

        }


        private void AddProjectile(Vector2 position)
        {
            Projectile projectile = new Projectile();
            projectile.Initialize(GraphicsDevice.Viewport, projectileTexture, position);
            projectiles.Add(projectile);
        }

        private void UpdateProjectiles()
        {
            // Update the Projectiles
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                projectiles[i].Update();

                if (projectiles[i].Active == false)
                {
                    projectiles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        /// 

        private void UpdateCollision()
        {
            // Use the Rectangle's built-in intersect function to 
            // determine if two objects are overlapping

            // Do the collision between the LocalPlayer and the RemotePlayer
            for (int i = 0; i < projectiles.Count; i++)
            {
                // Create the rectangles we need to determine if we collided with each other
                var rectangle1 = new Rectangle((int)projectiles[i].Position.X -
                projectiles[i].Width / 2, (int)projectiles[i].Position.Y -
                projectiles[i].Height / 2, projectiles[i].Width, projectiles[i].Height);

                var rectangle2 = new Rectangle((int)RemotePlayer.Position.X - RemotePlayer.Width/2 ,
                (int)RemotePlayer.Position.Y - RemotePlayer.Height/2,
                RemotePlayer.Width,RemotePlayer.Height);

                if (rectangle1.Intersects(rectangle2))
                {
                    RemotePlayer.Health -= projectiles[i].Damage;
                    projectiles[i].Active = false;
                }

            }
        }

        protected override void Draw(GameTime gameTime)
        {
         //   GraphicsDevice.Clear(Color.CornflowerBlue);

         //   spriteBatch.Begin();

         //   spriteBatch.Draw(mainBackground, Vector2.Zero, Color.White);

         //   // Draw the moving background
         //   bgLayer1.Draw(spriteBatch);
         //   bgLayer2.Draw(spriteBatch);

         //   LocalPlayer.Draw(spriteBatch);
         //   RemotePlayer.Draw(spriteBatch);

         //   // Draw the Projectiles
         //   for (int i = 0; i < projectiles.Count; i++)
         //   {
         //       projectiles[i].Draw(spriteBatch);
         //   }

         //   // Draw the score
         ////   spriteBatch.DrawString(font, "score: " + score, new Vector2(GraphicsDevice.Viewport.TitleSafeArea.X, GraphicsDevice.Viewport.TitleSafeArea.Y), Color.White);
         //   // Draw the player health
         //   spriteBatch.DrawString(font, "Your health: " + LocalPlayer.Health, new Vector2(GraphicsDevice.Viewport.TitleSafeArea.X+10, GraphicsDevice.Viewport.TitleSafeArea.Y + 10), Color.White);

         //   spriteBatch.DrawString(font, " health of enemy : " + RemotePlayer.Health, new Vector2(GraphicsDevice.Viewport.TitleSafeArea.Width-250, GraphicsDevice.Viewport.TitleSafeArea.Y + 10), Color.White);

         //   spriteBatch.End();


         // Only draw when game is active
            if (this.IsActive)
            {
                // Based on the current game state,
                // call the appropriate method
                switch (currentGameState)
                {
                    case GameState.SignIn:
                    case GameState.FindSession:
                    case GameState.CreateSession:
                        GraphicsDevice.Clear(Color.DarkBlue);
                        break;

                    case GameState.Start:
                        DrawStartScreen();
                        break;
                    case GameState.InGame:
                        DrawInGameScreen(gameTime);
                        break;
                    case GameState.GameOver:
                        DrawGameOverScreen();
                        break;
                }
            }


            base.Draw(gameTime);
        }

        private void DrawStartScreen()
        {
            // Clear screen
            GraphicsDevice.Clear(Color.AliceBlue);
            // Draw text for intro splash screen
            spriteBatch.Begin();
            // Draw instructions
            string text = "The dynamite player chases the gears\n";
            text += networkSession.Host.Gamertag +
                " is the HOST and plays as dynamite first";
            spriteBatch.DrawString(font, text,
                new Vector2((Window.ClientBounds.Width / 2)
                - (font.MeasureString(text).X / 2),
                (Window.ClientBounds.Height / 2)
                - (font.MeasureString(text).Y / 2)),
                Color.SaddleBrown);
            // If both gamers are there, tell gamers to press space bar or Start to begin
            if (networkSession.AllGamers.Count == 2)
            {
                text = "(Game is ready. Press Spacebar or Start button to begin)";
                spriteBatch.DrawString(font, text,
                    new Vector2((Window.ClientBounds.Width / 2)
                    - (font.MeasureString(text).X / 2),
                    (Window.ClientBounds.Height / 2)
                    - (font.MeasureString(text).Y / 2) + 60),
                    Color.SaddleBrown);
            }
            // If only one player is there, tell gamer you're waiting for players
            else
            {
                text = "(Waiting for players)";
                spriteBatch.DrawString(font, text,
                    new Vector2((Window.ClientBounds.Width / 2)
                    - (font.MeasureString(text).X / 2),
                    (Window.ClientBounds.Height / 2) + 60),
                    Color.SaddleBrown);
            }
            // Loop through all gamers and get their gamertags,
            // then draw list of all gamers currently in the game
            text = "\n\nCurrent Player(s):";
            foreach (Gamer gamer in networkSession.AllGamers)
            {
                text += "\n" + gamer.Gamertag;
            }
            spriteBatch.DrawString(font, text,
                new Vector2((Window.ClientBounds.Width / 2)
                - (font.MeasureString(text).X / 2),
                (Window.ClientBounds.Height / 2) + 90),
                Color.SaddleBrown);
            spriteBatch.End();
        }

        private void DrawInGameScreen(GameTime gameTime)
        {
            // Clear device
            GraphicsDevice.Clear(Color.White);
            spriteBatch.Begin();
            // Loop through all gamers in session
            foreach (NetworkGamer gamer in networkSession.AllGamers)
            {
                // Pull out the sprite for each gamer and draw it
                Player sprite = ((Player)gamer.Tag);
                sprite.Draw(spriteBatch);

            }
            spriteBatch.End();
        }

        private void DrawGameOverScreen()
        {
            // Clear device
            GraphicsDevice.Clear(Color.Navy);
            spriteBatch.Begin();
            // Game over. Find the chased sprite and draw his score.
            string text = "Game Over\n";
            foreach (NetworkGamer gamer in networkSession.AllGamers)
            {
                Player sprite = ((Player)gamer.Tag);

            }
            // Give players instructions from here
            text += "\nPress ENTER or A button to switch and play again";
            text += "\nPress ESCAPE or B button to exit to game lobby";
            spriteBatch.DrawString(font, text,
                new Vector2((Window.ClientBounds.Width / 2)
                - (font.MeasureString(text).X / 2),
                (Window.ClientBounds.Height / 2)
                - (font.MeasureString(text).Y / 2)),
                Color.WhiteSmoke);
            spriteBatch.End();
        }

    }
}
