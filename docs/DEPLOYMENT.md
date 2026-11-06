# Deployment

The brief requires the app to be deployed. Both options below work with MongoDB Atlas.

## Option A: Azure App Service (from Visual Studio)

1. Right-click **GeopoliticsGame.Web → Publish… → Azure → Azure App Service (Linux or Windows)**.
2. Sign in, then **Create new**: pick a name (e.g. `geopolitics-yourname`), a resource group,
   and the **Free F1** or **B1** plan → **Finish → Publish**.
3. In the Azure Portal → your App Service → **Settings → Environment variables**, add:

   | Name | Value |
   |---|---|
   | `Database__ConnectionString` | your Atlas `mongodb+srv://…` string |
   | `Database__Provider` | `Mongo` |
   | `Database__AllowMemoryFallback` | `false` |
   | `Ai__ApiKey` | *(optional)* your LLM key |

4. In **Atlas → Network Access**, allow the App Service's outbound IPs (*App Service →
   Networking → Outbound addresses*), or `0.0.0.0/0` for the exam.
5. Restart the app and browse to `https://<name>.azurewebsites.net`. The footer should show **MongoDB**.

> Data-protection keys (used for the login cookies) are created at startup. On a single
> instance that's fine. If you scale out, persist them (see *ASP.NET Core Data Protection*).

## Option B: Docker (Render, Railway, Fly.io, any VM)

Create `Dockerfile` in the repository root:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/GeopoliticsGame.Web -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GeopoliticsGame.Web.dll"]
```

On Render: *New → Web Service → from repo → Docker*. Add the same environment variables as in the
table above.

## After deploying (for the submission)

- Deployed URL: `https://…`
- App account: sign up on the live site and include the username and password.
- Database access: see `DATABASE.md` → *Giving the markers access*.
