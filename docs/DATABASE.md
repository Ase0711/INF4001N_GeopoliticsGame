# Database (MongoDB)

## Why MongoDB

The brief asks for a NoSQL database. A game is one self-contained, nested object: players,
10 territories, the turn order, an in-progress attack, and a running battle log. That fits a
single MongoDB **document**. Loading or saving a game is one read or one write, with no joins.

## Collections

| Collection | Document | Notes |
|---|---|---|
| `users` | `{ _id, username, email, passwordHash, createdAt, stats: { gamesPlayed, gamesWon, gamesLost, countriesConquered, battlesWon, battlesLost } }` | unique indexes `ux_username`, `ux_email`; stats updated with atomic `$inc` |
| `games` | `{ _id, ownerUserId, ownerUsername, humanCountryCode, status, winnerId, round, turnOrder[], currentTurnIndex, hasAttackedThisTurn, pendingAttack, players[], board[], log[], stats, createdAt, updatedAt, finishedAt }` | indexes `ix_owner_updated` (ownerUserId + updatedAt desc) and `ix_status` |
| `countries` | `{ _id: "ZAF", name, population, flag }` | reference catalogue, seeded on first run |

Enums are stored as readable strings (`"active"`, `"npc"`, `"aggressive"`), and field names are
camelCase. This mapping uses driver *conventions* (`Infrastructure/Mongo/MongoContext.cs`), so the
Core domain classes carry no MongoDB attributes.

## Separation from the rest of the app

```
Controllers → Services → IGameRepository / IUserRepository / ICountryRepository   (Core)
                                   ▲
            MongoGameRepository … (Web/Infrastructure/Mongo)   or   Memory…Repository (fallback)
```

`DatabaseServiceCollectionExtensions.AddGameDatabase` chooses the implementation at startup:

| `Database:Provider` | Behaviour |
|---|---|
| `Auto` (default) | MongoDB if `ConnectionString` is set **and** answers a ping; otherwise in-memory |
| `Mongo` | MongoDB, and fail at startup if it's unreachable (set `AllowMemoryFallback=false`) |
| `Memory` | always in-memory (demos and tests) |

For the deployed site, set `Database__Provider=Mongo` and `Database__AllowMemoryFallback=false`,
so a bad connection string fails loudly instead of silently using memory.

## Setting up MongoDB Atlas

1. **Create a cluster:** sign up at https://www.mongodb.com/atlas → *Create* → **M0 Free**.
2. **Database Access** → *Add New Database User* → username and password, role *Read and write to any database*.
3. **Network Access** → *Add IP Address* → your IP (and your host's outbound IPs, or `0.0.0.0/0` for the exam).
4. **Connect** → *Drivers* → copy `mongodb+srv://<user>:<password>@cluster0.xxxxx.mongodb.net/`.
5. In Visual Studio: right-click **GeopoliticsGame.Web → Manage User Secrets**:
   ```json
   { "Database": { "ConnectionString": "mongodb+srv://user:pass@cluster0.xxxxx.mongodb.net/" } }
   ```
6. Run the app. Expect `Connected to MongoDB database 'geopolitics'` in the output and a green **MongoDB** badge in the footer.

## Giving the markers access

The brief requires either database credentials or access for:
`ammarcanani@gmail.com`, `mc.tanner@uct.ac.za`, `ridewaan.hanslo@uct.ac.za`.

- **Option A (project access):** Atlas → *Project* → **Access Manager** → *Invite to Project* →
  add the three emails with the **Project Read Only** role. They can then browse the collections
  in *Data Explorer*.
- **Option B (database user):** *Database Access* → add a user (e.g. `marker`) with **Read Only**
  (`readAnyDatabase`) and put its username, password and connection string in your submission.

## Local MongoDB (optional)

```bash
docker run -d -p 27017:27017 --name geo-mongo mongo:7
# user secret: "Database": { "ConnectionString": "mongodb://localhost:27017" }
```
