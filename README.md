# weissdigital-my-commander

`myc` is a dual-panel terminal file manager. The plan is in [plan/](plan/README.md).

```bash
dotnet test src/Myc.sln
dotnet run --project src/Myc.App
```

The app shows two panels. The left starts in the current directory and the right starts at home. `Tab` switches panels. `F10` quits.