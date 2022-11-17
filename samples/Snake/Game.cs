using System;
using System.Runtime.InteropServices;
using Thread = System.Threading.Thread;

struct Game
{
    enum Result
    {
        Win, Loss
    }

    private Random _random;

    private Game(uint randomSeed)
    {
        _random = new Random(randomSeed);
    }

    private Result Run(ref FrameBuffer fb)
    {
        Snake s = new Snake(
            (byte)(_random.Next() % FrameBuffer.Width),
            (byte)(_random.Next() % FrameBuffer.Height),
            (Snake.Direction)(_random.Next() % 4));

        MakeFood(s, out byte foodX, out byte foodY);

        long gameTime = Environment.TickCount64;

        while (true)
        {
            fb.Clear();

            if (!s.Update())
            {
                s.Draw(ref fb);
                return Result.Loss;
            }

            s.Draw(ref fb);

            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo ki = Console.ReadKey(intercept: true);
                switch (ki.Key)
                {
                    case ConsoleKey.UpArrow:
                        s.Course = Snake.Direction.Up; break;
                    case ConsoleKey.DownArrow:
                        s.Course = Snake.Direction.Down; break;
                    case ConsoleKey.LeftArrow:
                        s.Course = Snake.Direction.Left; break;
                    case ConsoleKey.RightArrow:
                        s.Course = Snake.Direction.Right; break;
                }
            }

            if (s.HitTest(foodX, foodY))
            {
                if (s.Extend())
                    MakeFood(s, out foodX, out foodY);
                else
                    return Result.Win;
            }

            fb.SetPixel(foodX, foodY, '*');

            fb.Render();

            gameTime += 100;

            long delay = gameTime - Environment.TickCount64;
            if (delay >= 0)
                Thread.Sleep((int)delay);
            else
                gameTime = Environment.TickCount64;
        }
    }

    void MakeFood(in Snake snake, out byte foodX, out byte foodY)
    {
        do
        {
            foodX = (byte)(_random.Next() % FrameBuffer.Width);
            foodY = (byte)(_random.Next() % FrameBuffer.Height);
        }
        while (snake.HitTest(foodX, foodY));
    }

    public static void Main()
    {
#if WINDOWS
        Console.SetWindowSize(FrameBuffer.Width, FrameBuffer.Height + 1);
        Console.SetBufferSize(FrameBuffer.Width, FrameBuffer.Height + 1);
        Console.Title = "See Sharp Snake";
        Console.CursorVisible = false;
#endif

        FrameBuffer fb = new FrameBuffer();

        while (true)
        {
            Game g = new Game((uint)Environment.TickCount64);
            Result result = g.Run(ref fb);

            string message = result == Result.Win ? "You win" : "You lose";

            int position = (FrameBuffer.Width - message.Length) / 2;
            for (int i = 0; i < message.Length; i++)
            {
                fb.SetPixel(position + i, FrameBuffer.Height / 2, message[i]);
            }

            fb.Render();

            Console.ReadKey(intercept: true);
        }
    }
}
