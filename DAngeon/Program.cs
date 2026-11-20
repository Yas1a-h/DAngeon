using System.Text;

Console.CursorVisible = false;
Console.OutputEncoding = Encoding.UTF8;

while (true) // Full game restart loop ('R')
{
    int floor = 1;
    int hp = 100;
    const int maxHp = 100;

    Item weapon = new("Fists", ItemType.Weapon, 5);
    Item armor = new("Rags", ItemType.Armor, 0);

    bool gameOver = false;
    bool restartRun = false;
    string log = "WASD: Move/Dodge | E or Space: Attack adjacent enemy (M)!";

    while (true) // Floor loop
    {
        int width = Math.Max(30, (Console.WindowWidth - 6) / 2);
        int height = Math.Max(14, Console.WindowHeight - 8);
        int viewRadius = 6;

        var map = GenerateMap(width, height);
        var explored = new bool[height, width];

        var player = FindRandomFloor(map);
        var exit = FindRandomFloor(map);

        var loot = new List<LootDrop>
        {
            new(FindRandomFloor(map), RollSword(floor)),
            new(FindRandomFloor(map), RollArmor(floor)),
            new(FindRandomFloor(map), new Item("Healing Potion", ItemType.Potion, 35)),
            new(FindRandomFloor(map), new Item("Healing Potion", ItemType.Potion, 35)),
            new(FindRandomFloor(map), new Item("Healing Potion", ItemType.Potion, 35))
        };

        var enemies = new List<Enemy>();
        int enemyCount = 8 + floor * 2;
        for (int i = 0; i < enemyCount; i++)
        {
            var pos = FindRandomFloor(map);
            enemies.Add(new Enemy(
                X: pos.x,
                Y: pos.y,
                Hp: 30 + floor * 12,
                Damage: 10 + floor * 3,
                CanAct: true
            ));
        }

        int ghostTurns = 0;
        bool nextFloor = false;
        Console.Clear();

        while (!nextFloor) // Turn loop
        {
            // 1. Reveal fog of war
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int dx = x - player.x, dy = y - player.y;
                    if (dx * dx + dy * dy <= viewRadius * viewRadius)
                        explored[y, x] = true;
                }

            // 2. Render framed UI & map
            int innerWidth = width * 2;
            int totalWidth = innerWidth + 2;
            int padLeft = Math.Max(0, (Console.WindowWidth - totalWidth) / 2);
            int padTop = Math.Max(0, (Console.WindowHeight - (height + 6)) / 2);
            string margin = new(' ', padLeft);
            string hLine = new('═', innerWidth);

            Console.CursorVisible = false;
            Console.SetCursorPosition(0, padTop);

            WriteColored(margin + "╔" + hLine + "╗\n", ConsoleColor.DarkCyan);

            Console.Write(margin);
            WriteColored("║ ", ConsoleColor.DarkCyan);
            WriteColored($"FLOOR {floor}", ConsoleColor.Yellow);
            WriteColored("  |  ", ConsoleColor.DarkGray);

            int filledBars = (int)Math.Ceiling(hp / (double)maxHp * 10);
            string hpBar = new string('█', Math.Max(0, filledBars)) + new string('░', Math.Max(0, 10 - filledBars));
            ConsoleColor hpColor = hp > 50 ? ConsoleColor.Green : (hp > 25 ? ConsoleColor.Yellow : ConsoleColor.Red);
            WriteColored($"HP [{hpBar}] {hp}/{maxHp}", hpColor);

            WriteColored("  |  ", ConsoleColor.DarkGray);
            WriteColored($"WPN: {weapon.Name} ({weapon.Power} DMG)", ConsoleColor.Cyan);
            WriteColored("  |  ", ConsoleColor.DarkGray);
            WriteColored($"ARM: {armor.Name} (-{armor.Power} DEF)", ConsoleColor.Blue);
            WriteColored("  |  ", ConsoleColor.DarkGray);
            WriteColored($"FOES: {enemies.Count}", ConsoleColor.Magenta);

            string phaseTag = ghostTurns > 0 ? "  [DODGE READY!]" : "";
            if (ghostTurns > 0)
                WriteColored(phaseTag, ConsoleColor.Yellow);

            int usedLen = $" FLOOR {floor}  |  HP [{hpBar}] {hp}/{maxHp}  |  WPN: {weapon.Name} ({weapon.Power} DMG)  |  ARM: {armor.Name} (-{armor.Power} DEF)  |  FOES: {enemies.Count}{phaseTag}".Length;
            int rightPad = Math.Max(0, innerWidth - usedLen);
            Console.Write(new string(' ', rightPad));
            WriteColored("║\n", ConsoleColor.DarkCyan);

            WriteColored(margin + "╠" + hLine + "╣\n", ConsoleColor.DarkCyan);

            var rowBuffer = new StringBuilder(innerWidth);
            for (int y = 0; y < height; y++)
            {
                Console.Write(margin);
                WriteColored("║", ConsoleColor.DarkCyan);

                ConsoleColor currentColor = ConsoleColor.White;
                rowBuffer.Clear();

                for (int x = 0; x < width; x++)
                {
                    char ch;
                    ConsoleColor cellColor;

                    if (!explored[y, x])
                    {
                        ch = ' ';
                        cellColor = ConsoleColor.Black;
                    }
                    else
                    {
                        int dx = x - player.x, dy = y - player.y;
                        bool inSight = dx * dx + dy * dy <= viewRadius * viewRadius;
                        var drop = loot.FirstOrDefault(l => l.Pos == (x, y));

                        if (x == player.x && y == player.y)
                        {
                            ch = '@';
                            cellColor = ghostTurns > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                        }
                        else if (inSight && enemies.Any(e => e.X == x && e.Y == y))
                        {
                            ch = 'M';
                            cellColor = ConsoleColor.Magenta;
                        }
                        else if (drop is not null)
                        {
                            (ch, cellColor) = drop.Item.Type switch
                            {
                                ItemType.Weapon => ('†', ConsoleColor.Cyan),
                                ItemType.Armor => ('A', ConsoleColor.Blue),
                                ItemType.Potion => ('+', ConsoleColor.Yellow),
                                _ => ('?', ConsoleColor.White)
                            };
                        }
                        else if (x == exit.x && y == exit.y)
                        {
                            ch = 'E';
                            cellColor = ConsoleColor.Red;
                        }
                        else
                        {
                            ch = map[y, x];
                            cellColor = inSight ? ConsoleColor.White : ConsoleColor.DarkGray;
                        }
                    }

                    if (cellColor != currentColor && rowBuffer.Length > 0)
                    {
                        WriteColored(rowBuffer.ToString(), currentColor);
                        rowBuffer.Clear();
                    }
                    currentColor = cellColor;
                    rowBuffer.Append(ch).Append(' ');
                }

                if (rowBuffer.Length > 0)
                    WriteColored(rowBuffer.ToString(), currentColor);

                WriteColored("║\n", ConsoleColor.DarkCyan);
            }

            WriteColored(margin + "╠" + hLine + "╣\n", ConsoleColor.DarkCyan);

            Console.Write(margin);
            WriteColored("║ ", ConsoleColor.DarkCyan);
            string footer = $"{log}   [WASD: Move | E: Hit | R: Restart | Esc: Quit]";
            if (footer.Length > innerWidth - 1)
                footer = footer[..(innerWidth - 1)];
            ConsoleColor logColor = gameOver ? ConsoleColor.Red : ConsoleColor.White;
            WriteColored(footer.PadRight(innerWidth - 1), logColor);
            WriteColored("║\n", ConsoleColor.DarkCyan);

            WriteColored(margin + "╚" + hLine + "╝", ConsoleColor.DarkCyan);

            // 3. Input handling
            var key = Console.ReadKey(true).Key;
            while (Console.KeyAvailable)
                Console.ReadKey(true);
            Thread.Sleep(55);

            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.R)
            {
                Console.Clear();
                restartRun = true;
                break;
            }
            if (gameOver) continue;

            // 4. Dedicated Attack Button ('E' or 'Spacebar')
            if (key is ConsoleKey.E or ConsoleKey.Spacebar)
            {
                // Find any enemy standing directly next to player (Manhattan distance == 1)
                int adjIdx = enemies.FindIndex(e => Math.Abs(e.X - player.x) + Math.Abs(e.Y - player.y) == 1);

                if (adjIdx == -1)
                {
                    log = "No monster right next to you to hit!";
                    continue; // Swinging at air doesn't waste a turn
                }

                var enemy = enemies[adjIdx];
                int newEnemyHp = enemy.Hp - weapon.Power;
                string attackVerb = weapon.Name == "Fists" ? "Punched" : $"Slashed with {weapon.Name}";

                if (newEnemyHp <= 0)
                {
                    enemies.RemoveAt(adjIdx);
                    log = $"{attackVerb} and defeated monster! Enemies left: {enemies.Count}";
                }
                else
                {
                    enemies[adjIdx] = enemy with { Hp = newEnemyHp };
                    log = $"{attackVerb} for {weapon.Power} DMG! ({newEnemyHp} HP left)";
                }
            }
            else
            {
                // Movement & Dodge ('WASD' / Arrows)
                var (mx, my) = key switch
                {
                    ConsoleKey.W or ConsoleKey.UpArrow => (0, -1),
                    ConsoleKey.S or ConsoleKey.DownArrow => (0, 1),
                    ConsoleKey.A or ConsoleKey.LeftArrow => (-1, 0),
                    ConsoleKey.D or ConsoleKey.RightArrow => (1, 0),
                    _ => (0, 0)
                };

                if (mx == 0 && my == 0) continue;

                var target = (x: player.x + mx, y: player.y + my);
                int enemyIdx = enemies.FindIndex(e => e.X == target.x && e.Y == target.y);

                if (enemyIdx != -1)
                {
                    var beyond = (x: target.x + mx, y: target.y + my);
                    bool canSlipPast = ghostTurns > 0
                        && beyond.x > 0 && beyond.x < width - 1
                        && beyond.y > 0 && beyond.y < height - 1
                        && map[beyond.y, beyond.x] != '#'
                        && !enemies.Any(e => e.X == beyond.x && e.Y == beyond.y);

                    if (canSlipPast)
                    {
                        player = beyond;
                        ghostTurns = 0;
                        log = "Dodged right past the monster!";
                    }
                    else
                    {
                        log = "Monster blocks the way! Press E to attack or dodge when hit!";
                        continue; // Doesn't waste a turn bumping into enemy
                    }
                }
                else if (map[target.y, target.x] == '#')
                {
                    log = "Blocked by a wall!";
                    continue;
                }
                else
                {
                    player = target;
                }

                // Loot pickup
                int lootIdx = loot.FindIndex(l => l.Pos == player);
                if (lootIdx != -1)
                {
                    var picked = loot[lootIdx].Item;
                    loot.RemoveAt(lootIdx);

                    switch (picked.Type)
                    {
                        case ItemType.Weapon when picked.Power > weapon.Power:
                            weapon = picked;
                            log = $"Equipped {weapon.Name} ({weapon.Power} DMG)!";
                            break;
                        case ItemType.Weapon:
                            log = $"Ignored weaker {picked.Name} ({picked.Power} DMG).";
                            break;
                        case ItemType.Armor when picked.Power > armor.Power:
                            armor = picked;
                            log = $"Equipped {armor.Name} (-{armor.Power} incoming DMG)!";
                            break;
                        case ItemType.Armor:
                            log = $"Ignored weaker {picked.Name} (-{picked.Power} DEF).";
                            break;
                        case ItemType.Potion:
                            int healed = Math.Min(maxHp - hp, picked.Power);
                            hp += healed;
                            log = $"Drank {picked.Name}! Restored +{healed} HP.";
                            break;
                    }
                }

                if (player == exit)
                {
                    floor++;
                    ghostTurns = 0;
                    log = $"Descended to Floor {floor}! Monsters grew stronger...";
                    nextFloor = true;
                    continue;
                }
            }

            if (ghostTurns > 0) ghostTurns--;

            // 5. Enemy AI & attacks (act every 2nd turn)
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];

                if (!e.CanAct)
                {
                    enemies[i] = e with { CanAct = true };
                    continue;
                }

                int dx = player.x - e.X;
                int dy = player.y - e.Y;
                int distSq = dx * dx + dy * dy;

                (int ex, int ey) primary, secondary;

                if (distSq <= 36)
                {
                    if (Math.Abs(dx) >= Math.Abs(dy))
                    {
                        primary = (Math.Sign(dx), 0);
                        secondary = (0, Math.Sign(dy));
                    }
                    else
                    {
                        primary = (0, Math.Sign(dy));
                        secondary = (Math.Sign(dx), 0);
                    }
                }
                else
                {
                    (int, int)[] dirs = [(0, -1), (0, 1), (-1, 0), (1, 0)];
                    primary = dirs[Random.Shared.Next(dirs.Length)];
                    secondary = (0, 0);
                }

                var chosen = primary;
                if (chosen == (0, 0) || map[e.Y + chosen.ey, e.X + chosen.ex] == '#')
                    chosen = secondary;

                if (chosen == (0, 0))
                {
                    enemies[i] = e with { CanAct = false };
                    continue;
                }

                int nextX = e.X + chosen.ex;
                int nextY = e.Y + chosen.ey;

                if (nextX == player.x && nextY == player.y)
                {
                    int actualDamage = Math.Max(1, e.Damage - armor.Power);
                    hp -= actualDamage;
                    ghostTurns = 2;
                    log = $"Hit for {actualDamage} DMG! Press E to fight back or WASD to dodge!";

                    if (hp <= 0)
                    {
                        hp = 0;
                        log = $"YOU DIED on Floor {floor}! Press R to restart.";
                        gameOver = true;
                        break;
                    }

                    enemies[i] = e with { CanAct = false };
                }
                else if (map[nextY, nextX] != '#' && !enemies.Any(o => o.X == nextX && o.Y == nextY))
                {
                    enemies[i] = e with { X = nextX, Y = nextY, CanAct = false };
                }
                else
                {
                    enemies[i] = e with { CanAct = false };
                }
            }
        }

        if (restartRun) break;
    }
}

static void WriteColored(string text, ConsoleColor color)
{
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ResetColor();
}

static Item RollSword(int floor) => (floor + Random.Shared.Next(0, 2)) switch
{
    1 => new Item("Rusty Dagger", ItemType.Weapon, 20),
    2 => new Item("Iron Broadsword", ItemType.Weapon, 30),
    3 => new Item("Steel Katana", ItemType.Weapon, 42),
    4 => new Item("Runed Claymore", ItemType.Weapon, 55),
    _ => new Item("Dragon Slayer", ItemType.Weapon, 70)
};

static Item RollArmor(int floor) => (floor + Random.Shared.Next(0, 2)) switch
{
    1 => new Item("Leather Vest", ItemType.Armor, 4),
    2 => new Item("Studded Armor", ItemType.Armor, 8),
    3 => new Item("Chainmail", ItemType.Armor, 12),
    4 => new Item("Knight Plate", ItemType.Armor, 16),
    _ => new Item("Dragonscale", ItemType.Armor, 22)
};

static char[,] GenerateMap(int width, int height)
{
    var map = new char[height, width];
    for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            map[y, x] = Random.Shared.Next(100) < 45 ? '#' : '.';

    for (int step = 0; step < 4; step++)
    {
        var next = (char[,])map.Clone();
        for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
                next[y, x] = CountWalls(map, x, y) >= 5 ? '#' : '.';
        map = next;
    }

    for (int y = 0; y < height; y++)
        map[y, 0] = map[y, width - 1] = '#';
    for (int x = 0; x < width; x++)
        map[0, x] = map[height - 1, x] = '#';

    return map;
}

static int CountWalls(char[,] grid, int cx, int cy)
{
    int count = 0;
    for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if (grid[cy + dy, cx + dx] == '#') count++;
    return count;
}

static (int x, int y) FindRandomFloor(char[,] grid)
{
    while (true)
    {
        int x = Random.Shared.Next(1, grid.GetLength(1) - 1);
        int y = Random.Shared.Next(1, grid.GetLength(0) - 1);
        if (grid[y, x] == '.') return (x, y);
    }
}

enum ItemType { Weapon, Armor, Potion }
record Item(string Name, ItemType Type, int Power);
record LootDrop((int x, int y) Pos, Item Item);
record Enemy(int X, int Y, int Hp, int Damage, bool CanAct);