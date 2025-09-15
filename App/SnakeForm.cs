
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AffirmationImageGeneratorNice
{
    public class SnakeForm : Form
    {
        private Timer gameTimer;
        private PictureBox pbCanvas;
        private Label lblScore;
        private int score = 0;
        private int snakeSpeed = 10;
        private List<Circle> snake = new List<Circle>();
        private Circle food = new Circle();
        private int maxWidth;
        private int maxHeight;
        private int scoreIncrement = 10;

        private enum Direction
        {
            Up,
            Down,
            Left,
            Right
        }

        private Direction currentDirection = Direction.Right;

        public SnakeForm()
        {
            this.Text = "Snake Game";
            this.Width = 600;
            this.Height = 600;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.KeyDown += new KeyEventHandler(KeyIsDown);

            pbCanvas = new PictureBox();
            pbCanvas.Dock = DockStyle.Fill;
            pbCanvas.Paint += new PaintEventHandler(UpdateGraphics);
            this.Controls.Add(pbCanvas);

            lblScore = new Label();
            lblScore.Text = "Score: 0";
            lblScore.Font = new Font("Arial", 16);
            lblScore.Location = new Point(10, 10);
            this.Controls.Add(lblScore);
            lblScore.BringToFront();

            gameTimer = new Timer();
            gameTimer.Interval = 1000 / snakeSpeed;
            gameTimer.Tick += new EventHandler(GameTimerEvent);

            StartGame();
        }

        private void StartGame()
        {
            maxWidth = pbCanvas.Width / 10;
            maxHeight = pbCanvas.Height / 10;
            snake.Clear();
            score = 0;
            lblScore.Text = "Score: " + score;
            currentDirection = Direction.Right;

            Circle head = new Circle { X = 10, Y = 5 };
            snake.Add(head);

            for (int i = 0; i < 5; i++)
            {
                Circle body = new Circle();
                snake.Add(body);
            }

            GenerateFood();

            gameTimer.Start();
        }

        private void GenerateFood()
        {
            Random rnd = new Random();
            food = new Circle { X = rnd.Next(0, maxWidth), Y = rnd.Next(0, maxHeight) };
        }

        private void KeyIsDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left && currentDirection != Direction.Right)
            {
                currentDirection = Direction.Left;
            }
            if (e.KeyCode == Keys.Right && currentDirection != Direction.Left)
            {
                currentDirection = Direction.Right;
            }
            if (e.KeyCode == Keys.Up && currentDirection != Direction.Down)
            {
                currentDirection = Direction.Up;
            }
            if (e.KeyCode == Keys.Down && currentDirection != Direction.Up)
            {
                currentDirection = Direction.Down;
            }
        }

        private void GameTimerEvent(object sender, EventArgs e)
        {
            for (int i = snake.Count - 1; i >= 1; i--)
            {
                snake[i] = snake[i - 1];
            }

            switch (currentDirection)
            {
                case Direction.Right:
                    snake[0].X++;
                    break;
                case Direction.Left:
                    snake[0].X--;
                    break;
                case Direction.Up:
                    snake[0].Y--;
                    break;
                case Direction.Down:
                    snake[0].Y++;
                    break;
            }

            if (snake[0].X < 0 || snake[0].Y < 0 || snake[0].X >= maxWidth || snake[0].Y >= maxHeight)
            {
                EndGame();
            }

            for (int i = 1; i < snake.Count; i++)
            {
                if (snake[0].X == snake[i].X && snake[0].Y == snake[i].Y)
                {
                    EndGame();
                }
            }

            if (snake[0].X == food.X && snake[0].Y == food.Y)
            {
                EatFood();
            }

            pbCanvas.Invalidate();
        }

        private void EatFood()
        {
            score += scoreIncrement;
            lblScore.Text = "Score: " + score;
            Circle body = new Circle
            {
                X = snake[snake.Count - 1].X,
                Y = snake[snake.Count - 1].Y
            };
            snake.Add(body);
            GenerateFood();
        }

        private void EndGame()
        {
            gameTimer.Stop();
            MessageBox.Show("Game Over! Your score is: " + score);
            StartGame();
        }

        private void UpdateGraphics(object sender, PaintEventArgs e)
        {
            Graphics canvas = e.Graphics;
            Brush snakeColour;
            for (int i = 0; i < snake.Count; i++)
            {
                if (i == 0)
                {
                    snakeColour = Brushes.Black;
                }
                else
                {
                    snakeColour = Brushes.Green;
                }
                canvas.FillEllipse(snakeColour, new Rectangle(snake[i].X * 10, snake[i].Y * 10, 10, 10));
            }
            canvas.FillEllipse(Brushes.Red, new Rectangle(food.X * 10, food.Y * 10, 10, 10));
        }

        public class Circle
        {
            public int X { get; set; }
            public int Y { get; set; }

            public Circle()
            {
                X = 0;
                Y = 0;
            }
        }
    }
}
