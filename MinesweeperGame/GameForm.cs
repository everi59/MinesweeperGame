using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace MinesweeperGame
{
    public partial class GameForm : Form
    {
        private SolidBrush fieldBrush = new SolidBrush(Color.Green);
        private Pen highlightPen = new Pen(Color.White, 2);
        private Pen gridPen = new Pen(Color.Black);

        private int playerX = 200;
        private int playerY = 200;
        private const int Speed = 10;

        private Image playerImage;
        private Image flagImage;
        private Image mineImage;

        private const int GridSize = 50;
        private const int Rows = 50;
        private const int Cols = 50;

        private int cameraX = 0;
        private int cameraY = 0;

        private bool moveUp = false;
        private bool moveDown = false;
        private bool moveLeft = false;
        private bool moveRight = false;

        private float rotationAngle = 0f;

        private List<Rectangle> obstacles = new List<Rectangle>();
        private Dictionary<Point, bool> flags = new Dictionary<Point, bool>();

        private int[,] neighborCounts;
        private bool[,] revealedCells;

        private System.Windows.Forms.Timer gameTimer;

        private enum GameState { Menu, Playing, GameOver }
        private GameState currentState = GameState.Menu;

        private Point? hoveredCell = null;
        private Rectangle goldCell;
        private bool gameWon = false;

        private Button playAgainButton;
        private Button startButton;

        private int maxFlags = 30;
        private int remainingFlags = 30;
        private Label flagCounterLabel;


        public GameForm()
        {
            this.Text = "Minesweeper";
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            playerImage = Image.FromFile("C:\\Users\\ilyak\\source\\repos\\MinesweeperGame\\MinesweeperGame\\player.png");
            flagImage = Image.FromFile("C:\\Users\\ilyak\\source\\repos\\MinesweeperGame\\MinesweeperGame\\flag.png");
            mineImage = Image.FromFile("C:\\Users\\ilyak\\source\\repos\\MinesweeperGame\\MinesweeperGame\\mine.png");

            startButton = new Button
            {
                Text = "Начать игру",
                Font = new Font("Arial", 24),
                BackColor = Color.LightGreen,
                ForeColor = Color.Black,
                Size = new Size(300, 100),
                Location = new Point(600, 400)
            };

            startButton.Click += StartButton_Click;

            this.Controls.Add(startButton);

            this.KeyDown += GameForm_KeyDown;
            this.KeyUp += GameForm_KeyUp;

            this.MouseMove += GameForm_MouseMove;
            this.MouseDown += GameForm_MouseDown;

            gameTimer = new System.Windows.Forms.Timer();
            gameTimer.Interval = 25;
            gameTimer.Tick += GameTimer_Tick;

            GenerateObstacles();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            currentState = GameState.Playing;
            ResetGame();
            this.Controls.Remove(startButton);
            gameTimer.Start();
        }


        private void ResetGame()
        {
            playerX = 200;
            playerY = 200;
            gameWon = false;
            currentState = GameState.Playing;
            remainingFlags = maxFlags;

            flagCounterLabel = new Label
            {
                Text = $"Флажки: {remainingFlags}",
                Font = new Font("Arial", 16),
                ForeColor = Color.White,
                BackColor = Color.Black,
                AutoSize = true,
                Location = new Point(10, 10)
            };

            this.Controls.Add(flagCounterLabel);

            cameraX = playerX - this.ClientSize.Width / 2 + playerImage.Width / 2;
            cameraY = playerY - this.ClientSize.Height / 2 + playerImage.Height / 2;

            cameraX = Math.Max(0, Math.Min(Cols * GridSize - this.ClientSize.Width, cameraX));
            cameraY = Math.Max(0, Math.Min(Rows * GridSize - this.ClientSize.Height, cameraY));

            revealedCells = new bool[Cols, Rows];

            int startX = playerX / GridSize;
            int startY = playerY / GridSize;

            obstacles.Clear();

            GenerateObstacles();

            CalculateNeighborCounts();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = startX + dx;
                    int ny = startY + dy;

                    if (nx >= 0 && nx < Cols && ny >= 0 && ny < Rows)
                    {
                        revealedCells[nx, ny] = true;

                        if (neighborCounts[nx, ny] == 0)
                        {
                            FloodFillReveal(nx, ny);
                        }
                    }
                }
            }

            flags.Clear();

            moveUp = false; 
            moveDown = false;
            moveLeft = false;
            moveRight = false;
        }
        
        private void GameOver()
        {
            currentState = GameState.GameOver;
            ShowGameOverMenu();
            gameTimer.Stop();
        }

        private void WinGame()
        {
            gameWon = true;
            ShowWinScreen();
            gameTimer.Stop();
        }

        private void GameForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (currentState != GameState.Playing) return;

            if (e.KeyCode == Keys.W || e.KeyCode == Keys.Up) moveUp = true;
            if (e.KeyCode == Keys.S || e.KeyCode == Keys.Down) moveDown = true;
            if (e.KeyCode == Keys.A || e.KeyCode == Keys.Left) moveLeft = true;
            if (e.KeyCode == Keys.D || e.KeyCode == Keys.Right) moveRight = true;
        }

        private void GameForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (currentState != GameState.Playing) return;

            if (e.KeyCode == Keys.W || e.KeyCode == Keys.Up) moveUp = false;
            if (e.KeyCode == Keys.S || e.KeyCode == Keys.Down) moveDown = false;
            if (e.KeyCode == Keys.A || e.KeyCode == Keys.Left) moveLeft = false;
            if (e.KeyCode == Keys.D || e.KeyCode == Keys.Right) moveRight = false;
        }

        private void GameForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (currentState != GameState.Playing) return;

            int mouseWorldX = e.X + cameraX;
            int mouseWorldY = e.Y + cameraY;

            float deltaX = mouseWorldX - (playerX + playerImage.Width / 2);
            float deltaY = mouseWorldY - (playerY + playerImage.Height / 2);

            rotationAngle = (float)(Math.Atan2(deltaY, deltaX) * (180 / Math.PI));
            int cellX = (mouseWorldX) / GridSize;
            int cellY = (mouseWorldY) / GridSize;

            if (cellX >= 0 && cellX < Cols && cellY >= 0 && cellY < Rows)
            {
                hoveredCell = new Point(cellX, cellY);
            }
            else
            {
                hoveredCell = null;
            }
        }

        private void GameForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (currentState != GameState.Playing || gameWon) return;

            if (e.Button == MouseButtons.Left)
            {
                int cellX = (e.X + cameraX) / GridSize;
                int cellY = (e.Y + cameraY) / GridSize;
                var cellPos = new Point(cellX, cellY);

                if (flags.ContainsKey(cellPos))
                {
                    flags.Remove(cellPos);
                    remainingFlags++;
                }
                else
                {
                    if (remainingFlags > 0)
                    {
                        flags[cellPos] = true;
                        remainingFlags--;
                    }
                }

                flagCounterLabel.Text = $"Флажки: {remainingFlags}";
                this.Invalidate();
            }
        }

        private void GameTimer_Tick(object sender, EventArgs e)
        {
            if (gameWon || currentState != GameState.Playing) return;


            if (moveUp) playerY -= Speed;
            if (moveDown) playerY += Speed;
            if (moveLeft) playerX -= Speed;
            if (moveRight) playerX += Speed;

            playerX = Math.Max(0, Math.Min(Cols * GridSize - playerImage.Width, playerX));
            playerY = Math.Max(0, Math.Min(Rows * GridSize - playerImage.Height, playerY));

            Rectangle playerRect = new Rectangle(playerX, playerY, playerImage.Width, playerImage.Height);

            foreach (var obstacle in obstacles)
            {
                if (playerRect.IntersectsWith(obstacle))
                {
                    int cellX = obstacle.X / GridSize;
                    int cellY = obstacle.Y / GridSize;
                    var cellPos = new Point(cellX, cellY);

                    if (!flags.ContainsKey(cellPos))
                    {
                        GameOver();
                        return;
                    }
                }
            }

            if (playerRect.IntersectsWith(goldCell))
            {
                WinGame();
                return;
            }

            UpdatePlayerPosition();

            UpdateCamera();

            this.Invalidate();
        }

        private void UpdatePlayerPosition()
        {
            HashSet<Point> cellsUnderPlayer = new HashSet<Point>();
            for (int dx = 0; dx < 3; dx++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    int cellX = (playerX + dx * GridSize) / GridSize;
                    int cellY = (playerY + dy * GridSize) / GridSize;
                    cellsUnderPlayer.Add(new Point(cellX, cellY));
                }
            }

            bool revealedNewCells = false;
            foreach (Point cell in cellsUnderPlayer)
            {
                if (cell.X >= 0 && cell.X < Cols && cell.Y >= 0 && cell.Y < Rows)
                {
                    if (!revealedCells[cell.X, cell.Y])
                    {
                        revealedCells[cell.X, cell.Y] = true;
                        revealedNewCells = true;

                        if (neighborCounts[cell.X, cell.Y] == 0)
                        {
                            FloodFillReveal(cell.X, cell.Y);
                        }
                    }
                }
            }

            if (revealedNewCells)
            {
                this.Invalidate();
            }
        }

        private void FloodFillReveal(int startX, int startY)
        {
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(startX, startY));

            while (queue.Count > 0)
            {
                Point current = queue.Dequeue();

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = current.X + dx;
                        int ny = current.Y + dy;

                        if (nx >= 0 && nx < Cols && ny >= 0 && ny < Rows && !revealedCells[nx, ny])
                        {
                            revealedCells[nx, ny] = true;

                            if (neighborCounts[nx, ny] == 0)
                            {
                                queue.Enqueue(new Point(nx, ny));
                            }
                        }
                    }
                }
            }
        }


        private void ShowWinScreen()
        {
            Label winLabel = new Label
            {
                Text = "Вы победили!",
                Font = new Font("Arial", 36, FontStyle.Bold),
                ForeColor = Color.Gold,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(this.ClientSize.Width / 2 - 150, this.ClientSize.Height / 2 - 100)
            };
            this.Controls.Add(winLabel);

            playAgainButton = new Button
            {
                Text = "Играть еще",
                Font = new Font("Arial", 24),
                BackColor = Color.Gold,
                ForeColor = Color.Black,
                Size = new Size(200, 80),
                Location = new Point(this.ClientSize.Width / 2 - 100, this.ClientSize.Height / 2)
            };

            playAgainButton.Click += (s, e) => {
                this.Controls.Clear();
                ResetGame();
                gameTimer.Start();
            };

            this.Controls.Add(playAgainButton);
        }

        private void ShowGameOverMenu()
        {
            Button restartButton = new Button
            {
                Text = "Перезапустить игру",
                Font = new Font("Arial", 24),
                BackColor = Color.LightBlue,
                ForeColor = Color.Black,
                Size = new Size(300, 100),
                Location = new Point(this.ClientSize.Width / 2 - 150, this.ClientSize.Height / 2 - 50)
            };

            restartButton.Click += RestartButton_Click;
            this.Controls.Add(restartButton);

        }
        private void RestartButton_Click(object sender, EventArgs e)
        {
            currentState = GameState.Playing;

            this.Controls.Clear();

            ResetGame();

            gameTimer.Start();
        }

        private void UpdateCamera()
        {
            float lerpFactor = 0.1f;
            int targetX = playerX - this.ClientSize.Width / 2 + playerImage.Width / 2;
            int targetY = playerY - this.ClientSize.Height / 2 + playerImage.Height / 2;

            cameraX = (int)(cameraX + (targetX - cameraX) * lerpFactor);
            cameraY = (int)(cameraY + (targetY - cameraY) * lerpFactor);

            cameraX = Math.Max(0, Math.Min(Cols * GridSize - this.ClientSize.Width, cameraX));
            cameraY = Math.Max(0, Math.Min(Rows * GridSize - this.ClientSize.Height, cameraY));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (currentState == GameState.Menu)
            {
                return;
            }

            DrawGrid(e.Graphics);

            DrawFlags(e.Graphics);

            DrawRotatedPlayer(e.Graphics);
        }

        private void DrawGrid(Graphics g)
        {
            int startCol = Math.Max(0, cameraX / GridSize);
            int startRow = Math.Max(0, cameraY / GridSize);
            int endCol = Math.Min(Cols, (cameraX + this.ClientSize.Width) / GridSize + 1);
            int endRow = Math.Min(Rows, (cameraY + this.ClientSize.Height) / GridSize + 1);


            for (int x = startCol; x < endCol; x++)
            {
                for (int y = startRow; y < endRow; y++)
                {
                    int worldX = x * GridSize;
                    int worldY = y * GridSize;
                    int screenX = worldX - cameraX;
                    int screenY = worldY - cameraY;

                    if (revealedCells[x, y])
                    {

                        g.FillRectangle(fieldBrush, screenX, screenY, GridSize, GridSize);

                        if (neighborCounts[x, y] > 0)
                        {
                            DrawNumber(g, neighborCounts[x, y], screenX, screenY);
                        }
                    }
                    else
                    {
                        g.FillRectangle(Brushes.DarkGray, screenX, screenY, GridSize, GridSize);
                    }

                    Rectangle cellRect = new Rectangle(worldX, worldY, GridSize, GridSize);
                    Rectangle playerRect = new Rectangle(playerX, playerY, playerImage.Width, playerImage.Height);

                    if (cellRect.IntersectsWith(playerRect))
                    {
                        g.DrawRectangle(highlightPen, screenX, screenY, GridSize, GridSize);
                    }
                    else
                    {
                        g.DrawRectangle(gridPen, screenX, screenY, GridSize, GridSize);
                    }
                }
            }

            if (currentState == GameState.GameOver || gameWon)
            {
                DrawAllMines(g);
            }

            if (hoveredCell.HasValue)
            {
                int x = hoveredCell.Value.X * GridSize - cameraX;
                int y = hoveredCell.Value.Y * GridSize - cameraY;
                g.DrawRectangle(new Pen(Color.Yellow, 2), x, y, GridSize, GridSize);
            }

            if (!gameWon && currentState == GameState.Playing)
            {
                g.FillRectangle(Brushes.Gold, goldCell.X - cameraX, goldCell.Y - cameraY, GridSize, GridSize);
                g.DrawRectangle(Pens.DarkGoldenrod, goldCell.X - cameraX, goldCell.Y - cameraY, GridSize, GridSize);
            }
        }

        private void DrawAllMines(Graphics g)
        {
            foreach (var obstacle in obstacles)
            {
                int x = obstacle.X / GridSize;
                int y = obstacle.Y / GridSize;
                int screenX = x * GridSize - cameraX;
                int screenY = y * GridSize - cameraY;

                g.DrawImage(mineImage, screenX, screenY, GridSize, GridSize);
            }
        }

        private void DrawFlags(Graphics g)
        {
            foreach (var flagPos in flags.Keys)
            {
                int x = flagPos.X * GridSize - cameraX;
                int y = flagPos.Y * GridSize - cameraY;

                g.DrawImage(flagImage, x, y, GridSize, GridSize);
            }
        }

        private void DrawNumber(Graphics g, int number, int screenX, int screenY)
        {
            Color numberColor = Color.Red;
            if (number == 1)
            {
                numberColor = Color.LightBlue;
            }
            if (number == 2)
            {
                numberColor = Color.Blue;
            }


            using (var font = new Font("Arial", 14, FontStyle.Bold))
            using (var brush = new SolidBrush(numberColor))
            {
                string text = number.ToString();

                SizeF textSize = g.MeasureString(text, font);
                float textX = screenX + (GridSize - textSize.Width) / 2;
                float textY = screenY + (GridSize - textSize.Height) / 2;

                g.DrawString(text, font, brush, textX, textY);
            }
        }

        private void DrawRotatedPlayer(Graphics g)
        {
            int screenX = playerX - cameraX + playerImage.Width / 2;
            int screenY = playerY - cameraY + playerImage.Height / 2;

            g.TranslateTransform(screenX, screenY);
            g.RotateTransform(rotationAngle);
            g.TranslateTransform(-playerImage.Width / 2, -playerImage.Height / 2);

            g.DrawImage(playerImage, 0, 0);

            g.ResetTransform();
        }

        private void GenerateObstacles()
        {
            Random random = new Random();
            int obstacleCount = 300;

            for (int i = 0; i < obstacleCount; i++)
            {
                int row, col;
                Rectangle obstacle;
                bool overlaps;
                bool startField;

                do
                {
                    row = random.Next(Rows);
                    col = random.Next(Cols);
                    obstacle = new Rectangle(col * GridSize, row * GridSize, GridSize, GridSize);

                    Rectangle playerStart = new Rectangle(200, 200, playerImage.Width, playerImage.Height);
                    overlaps = playerStart.IntersectsWith(obstacle);
                    startField = (2 < row && row < 8) && (2 < col && col < 8);
                }
                while (overlaps || startField);

                obstacles.Add(obstacle);
            }

            do
            {
                int row = random.Next(30, Rows);
                int col = random.Next(30, Cols);
                goldCell = new Rectangle(col * GridSize, row * GridSize, GridSize, GridSize);
            }
            while (obstacles.Any(o => o.IntersectsWith(goldCell)) ||
                   new Rectangle(200, 200, playerImage.Width, playerImage.Height).IntersectsWith(goldCell));

            CalculateNeighborCounts();
        }

        private void CalculateNeighborCounts()
        {
            neighborCounts = new int[Cols, Rows];

            bool[,] isMine = new bool[Cols, Rows];
            foreach (var obstacle in obstacles)
            {
                int x = obstacle.X / GridSize;
                int y = obstacle.Y / GridSize;
                isMine[x, y] = true;
            }

            for (int x = 0; x < Cols; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    if (!isMine[x, y])
                    {
                        int count = 0;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                int nx = x + dx;
                                int ny = y + dy;

                                if (nx >= 0 && nx < Cols && ny >= 0 && ny < Rows && isMine[nx, ny])
                                {
                                    count++;
                                }
                            }
                        }
                        neighborCounts[x, y] = count;
                    }
                    else
                    {
                        neighborCounts[x, y] = -1;
                    }
                }
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GameForm());
        }
    }
}
