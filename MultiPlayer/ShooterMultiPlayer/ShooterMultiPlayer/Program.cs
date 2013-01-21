using System;

namespace ShooterMultiPlayer
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (MultiGame game = new MultiGame())
            {
                game.Run();
            }
        }
    }
#endif
}

