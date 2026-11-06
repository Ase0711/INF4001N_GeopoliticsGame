# Geopolitics Game

A turn-based strategy game developed for the INF4001 entrance exam. Choose a country and compete against nine computer-controlled countries (NPCs). The aim is to conquer all the territories on the map.

## What You'll Need
- Visual Studio 2022 with the ASP.NET and web development workload, or the .NET 8 SDK.
- An internet connection to access MongoDB Atlas.

## How to Run the Application
1. Extract the ZIP folder.
2. Open `GeopoliticsGame.sln` in Visual Studio.
3. Make sure `GeopoliticsGame.Web` is set as the Startup Project.
4. Press **Ctrl + F5** to run the application.
5. Alternatively, you can run it from the terminal using:

   dotnet run --project src/GeopoliticsGame.Web

6. Once it starts, open the URL displayed in the terminal.

## Database Details

The application uses MongoDB Atlas.

- **Username:** `s230184480_db_user`
- **Password:** `uDVzZGPkKZDzuWCO`
- **Cluster:** `Cluster0`
- **Cluster Address:** `cluster0.z8ch5ii.mongodb.net`
- **Database:** `geopolitics`

The database connection is already configured in the application.

If the application cannot connect to MongoDB Atlas, it can switch to an in-memory database for local testing. The footer will indicate when this happens. Please note that data saved in memory will not be retained after restarting the application.

## Deployment & Access Details

- **Live Application URL:** https://inf4001n-geopoliticsgame.onrender.com/
- **Application Test Account:**
  - **Username:** `admin`
  - **Email:** `admin@myuct.ac.za`
  - **Password:** `Testing123!`

## Database Access
The MongoDB Atlas database has been configured with read/write access. 
Invited accounts:
- `ammarcanani@gmail.com`
- `mc.tanner@uct.ac.za`
- `ridewaan.hanslo@uct.ac.za`

Dedicated Database Credentials:
Database Name: `geopolitics`.
Password (for all users below): `Testing123!`
Usernames:
- `ammarcanani_db_user`
- `mctanner_db_user`
- `ridewaanhanslo_db_user`

## How to Run Locally
1. Clone the repository:
   ```bash
   git clone [https://github.com/Ase0711/INF4001N_GeopoliticsGame.git](https://github.com/Ase0711/INF4001N_GeopoliticsGame.git)
