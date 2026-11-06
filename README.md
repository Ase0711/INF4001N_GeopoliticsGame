# 🌍 Geopolitics — Conquer the World

A single-player, Risk-style strategy game for the **UCT INF4001 Entrance Exam (2027)**.
Pick a country, face **9 AI rivals** that play to win, and conquer all **10 nations**.
Battles are fought die by die and narrated by an **AI war correspondent**. Anyone with the
link can watch a game live; only the signed-in owner can play it.

**Stack:** C# / **ASP.NET Core 8 MVC** · **MongoDB** (NoSQL, e.g. MongoDB Atlas) · Razor views + Bootstrap · vanilla JS · xUnit

---

## ▶️ Run it in Visual Studio

**Requirements:** Visual Studio 2022 **17.8 or later** with the *ASP.NET and web development* workload (includes the .NET 8 SDK).

1. Open **`GeopoliticsGame.sln`**.
2. Right-click **GeopoliticsGame.Web** → **Set as Startup Project**.
3. Press **F5** (or Ctrl+F5). NuGet packages restore automatically and the browser opens the site.
4. Click **Sign up**, create an account, choose a country and press **Start campaign**.

No database configured yet? The app still runs: it uses a built-in **in-memory** store and
shows a yellow **In-memory** badge in the footer. Data is lost when you stop it.
Connect MongoDB (below) for real, persistent storage. The footer badge then turns green: **MongoDB**.

**Tests:** *Test → Run All Tests* (Test Explorer). 54 tests cover the soldier bands, dice
pairing, conquest rewards, turn rules, NPC decisions, and full simulated AI-vs-AI games.

<details><summary>Command line instead of Visual Studio</summary>

```bash
dotnet run --project src/GeopoliticsGame.Web     # http://localhost:5243
dotnet test
```
</details>

## 🗄️ Connect MongoDB (the online NoSQL database)

1. Create a free cluster on **MongoDB Atlas** (https://www.mongodb.com/atlas), add a database user,
   and allow your IP under *Network Access*.
2. Copy the connection string (`mongodb+srv://…`).
3. In Visual Studio right-click **GeopoliticsGame.Web → Manage User Secrets** and paste:
   ```json
   {
     "Database": { "ConnectionString": "mongodb+srv://USER:PASSWORD@cluster0.xxxxx.mongodb.net/" }
   }
   ```
   User secrets stay out of source control. In production, use the environment variable
   `Database__ConnectionString` instead.
4. Run again. The console logs `Connected to MongoDB database 'geopolitics'`, the
   `users`, `games` and `countries` collections are created, and the country list is seeded automatically.

Details, schema and how to give the markers access: [`docs/DATABASE.md`](docs/DATABASE.md).

## 🎙️ Live AI commentary (optional)

Without a key the game uses a built-in commentator, so everything still works. For live LLM
commentary, add to user secrets:

```json
{ "Ai": { "ApiKey": "sk-...", "Model": "gpt-4o-mini" } }
```

Any OpenAI-compatible API works (OpenAI, Groq, OpenRouter, a local Ollama). Set `Ai:BaseUrl` to
switch providers. If the AI is slow or down, the game falls back to the built-in commentator after
`Ai:TimeoutSeconds`, so play is never blocked.

---

## 🎮 Gameplay (as specified)

| Rule | Implementation |
|---|---|
| 10 countries: yours + 9 random | `GameEngine.CreateGame` |
| Soldiers from population (1–9 by band) | `SoldierAllocator` |
| Random first player | Turn order shuffled with a crypto-secure RNG |
| One attack per turn, or skip | Enforced by `GameEngine.StartAttack` |
| Transfers between own countries, not during an attack, keep ≥ 1 | `GameEngine.Transfer` |
| Need ≥ 2 soldiers; attack with soldiers − 1 | `BattleEngine.CanAttack` / `DiceCount` |
| Dice = least soldiers able to fight; die *i* vs die *i*; ties spare both | `BattleEngine.ResolveRound` (paired in roll order, as in the brief's example) |
| Roll until conquered, down to 1, or retreat (retreat ends the turn) | `RollAttack` / `Retreat` |
| Conquest reward `max(2, value ÷ 2)`, then optionally move soldiers in | `BattleEngine.ConquestReward` + "Move soldiers in" panel |
| From round 2: 2 reinforcements per country, allocated freely | `GrantReinforcements` / `Deploy` |
| Eliminated players are skipped; whoever holds all 10 wins | `AdvanceTurn` / `Conquer` |
| All countries and soldier counts always visible | World map + standings |
| Battles shown live: each attack, dice, losses, outcome | Battle stage + battle feed |
| LLM-generated commentary | `LlmCommentaryService` |
| NPCs play to win and explain every decision, also during fast-forward | `NpcStrategy` + `NpcTurnRunner` |
| Fast-forward speeds up NPC turns but still shows them | Replay frames played at 130 ms instead of ~1 s |
| Visitors can watch a shared link; only the owner can play | Public `GET`, owner-only moves (401/403) |

**Bonus features:** NPC personality profiles (aggressive / balanced / defensive) and **analytics**
for each player plus the whole platform (win rates, best starting countries, leaderboard). There
are also **anti-cheat** measures: attacks are saved server-side, so closing the browser mid-attack
changes nothing; dice come from a crypto RNG; and requests for the same game are serialised.

---

## 🏗️ Architecture

```
GeopoliticsGame.sln
├─ src/GeopoliticsGame.Core        ← domain + game engine (no web or database dependencies)
│   ├─ Domain/        GameState, Territory, Player, User, LogEntry …
│   ├─ Engine/        GameEngine, BattleEngine, NpcStrategy, NpcTurnRunner, SoldierAllocator, Dice
│   ├─ Abstractions/  IUserRepository, IGameRepository, ICountryRepository, ICommentaryService
│   └─ Data/          CountryCatalogue (seed data)
├─ src/GeopoliticsGame.Web         ← ASP.NET Core MVC
│   ├─ Controllers/   Home, Account, Games, Analytics (pages) + GameApiController (JSON)
│   ├─ Models/        view models + API requests (DataAnnotations validation)
│   ├─ Views/         Razor views
│   ├─ Services/      GameService, AccountService, AnalyticsService, commentary (LLM + fallback)
│   ├─ Infrastructure/
│   │   ├─ Mongo/     MongoContext + Mongo repositories  ← the database layer
│   │   └─ Memory/    in-memory repositories (offline fallback)
│   └─ wwwroot/       CSS + JS (game board client)
└─ tests/GeopoliticsGame.Tests     ← xUnit
```

- **MVC + layering:** Controllers → services → engine. Repositories are only reached through
  interfaces, so the database is fully separate from the rest of the app. Core doesn't reference
  ASP.NET or MongoDB.
- **OOP:** Each class has one job: `BattleEngine` (combat), `SoldierAllocator` (population bands),
  `NpcStrategy` (decisions), `NpcTurnRunner` (plays an NPC turn), and `GameEngine` (all rule
  checks plus every state change). Randomness sits behind `IDice` so tests can script exact rolls.
- **Server is authoritative:** the browser never decides outcomes. It only displays results and
  asks the API to make a move.

## 🔐 Security

- Cookie authentication (HttpOnly, SameSite) and `[Authorize]`, plus an ownership check on every move.
- Passwords hashed with **PBKDF2-SHA256** (210,000 iterations, per-user salt) and compared in constant time.
- **Anti-forgery tokens** on every form and API POST.
- **Rate limiting** on sign-in and sign-up.
- Server-side validation of all input.
- Security headers, including a strict CSP with no inline scripts.
- Open-redirect protection on the login return URL.

## 🚀 Deploying

See [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) (Azure App Service via Visual Studio **Publish**, or Docker).

## 📋 Submission checklist

- [x] Source code (zip this folder, without `bin/` and `obj/`)
- [x] README explaining how to run it (this file)
- [ ] Database access for the markers → [`docs/DATABASE.md`](docs/DATABASE.md#giving-the-markers-access)
- [ ] Deployed URL → [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)
- [ ] App username/password: sign up on the deployed site and include those details in your submission
