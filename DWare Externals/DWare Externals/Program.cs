using NAudio.Wave;
using SharpDX.DirectWrite;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;

namespace FurryWare;

class Program
{

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    struct ViewMatrix
    {
        public float m11, m12, m13, m14;
        public float m21, m22, m23, m24;
        public float m31, m32, m33, m34;
        public float m41, m42, m43, m44;
    }

    Vector4[] teamColor =
    {
        new Vector4(0f, 0f, 1f, 1f),        // blue   (#0000FF, alpha FF)
        new Vector4(0f, 1f, 0f, 1f),        // green  (#00FF00, alpha FF)
        new Vector4(1f, 1f, 0f, 1f),        // yellow (#FFFF00, alpha FF)
        new Vector4(1f, 0.5f, 0f, 1f),      // orange (#FF8000, alpha FF)
        new Vector4(0.75f, 0.30f, 0.75f, 1f),// purple (#BF4DBF, alpha FF)
        new Vector4(1f, 1f, 1f, 1f),        // white  (#FFFFFF, alpha FF)
    };

    Driver driver = new("cs2");
    Offsets offset = new();
    Settings settings = new();
    Entity localPlayer = new();
    FOverlay overlay = new();

    nint client;
    nint entityList;
    nint localPlayerPawn;
    nint localPlayerController;
    nint cameraService;

    Vector2 windowLocation;
    Vector2 windowSize;
    Vector2 lineOrigin;
    Vector2 lineOriginTop;
    Vector2 windowCenter;
    Vector2 topRightScreen;

    IWavePlayer outputDevice;
    AudioFileReader audioFile;

    int resX;
    int resY;

    int previousShootsFired = 0;
    bool isCrouched = false;
    int previousTotalHits = 0;
    bool isPlanted = false;
    ulong plantTime = 0;


    void MainThread()
    {
        ReadJSON();

        if (!overlay.InitWindow()) throw new Exception("Failed to initialize NVIDIA overlay window.");
        if (!overlay.InitD2D()) throw new Exception("Failed to initialize Direct2D.");

        resY = (int)overlay.ScreenHeight;
        resX = (int)overlay.ScreenWidth;

        client = driver.GetModuleBase("client.dll");

        entityList = driver.ReadMemory<nint>(client + offset.dwEntityList);
        localPlayerPawn = driver.ReadMemory<nint>(client + offset.dwLocalPlayerPawn);
        localPlayerController = driver.ReadMemory<nint>(client + offset.dwLocalPlayerController);
        cameraService = driver.ReadMemory<nint>(client + offset.m_pCameraServices);

        windowLocation = new(0, 0);
        windowSize = new(resX, resY);
        lineOrigin = new(resX / 2, resY);
        lineOriginTop = new(resX / 2, 0);
        windowCenter = new(resX / 2, resY / 2);
        topRightScreen = new(resX, 0);

        new Thread(Trigger).Start();
        new Thread(Mix).Start();
        new Thread(AimLockUpdate).Start();

        while (true)
        {
            overlay.BeginScene();
            overlay.ClearScene();

            if (GetAsyncKeyState(0x23) < 0)
            {
                WriteJSON();
                overlay.EndScene();
                Environment.Exit(0);
            }

            ESP();

            overlay.EndScene();
        }
    }

    //---------------------------------- READ / WRITE .json -------------------------------

    void ReadJSON()
    {
        var assembly = Assembly.GetExecutingAssembly();

        try{
            string JsonStringSettings = File.ReadAllText("settings.json");
            settings = JsonSerializer.Deserialize<Settings>(JsonStringSettings);
        }
        catch{
            using (Stream? stream = assembly.GetManifestResourceStream("FurryWare.settings.json"))
            using (StreamReader reader = new StreamReader(stream)){
                string JsonStringSettings = reader.ReadToEnd();
                settings = JsonSerializer.Deserialize<Settings>(JsonStringSettings);
            }
        }
    }

    void WriteJSON()
    {
        string JsonPath = "settings.json";
        if (!File.Exists(JsonPath))
            using (File.Create(JsonPath)) ;
        JsonSerializerOptions options = new() { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(settings, options);
        File.WriteAllText(JsonPath, jsonString);
    }

    //--------------------------------  ESP  --------------------------------

    void ESP()
    {
        foreach (var e in UpdateEntities()
         .Where(e => e.healt > 0 && e.currentWeaponIndex != 0 && IsPixelInsideScreen(e.orignScreenPosition, e.absScreenPosition))
         .OrderByDescending(e => e.distance))
            DrawVisuals(e);
        


        if (settings.enableAimFOV && settings.enableAim){
            overlay.SetColor(1f, 1f, 1f, 1f);
            overlay.DrawCircle(windowCenter.X, windowCenter.Y, settings.aimFOV, 2f);
        }
    }


    //-------------------------  UPDATE EntityLIst  -------------------------
    List<Entity> UpdateEntities()
    {
        List<Entity> playerPawnList = new List<Entity>();

        nint listEntry = driver.ReadMemory<nint>(entityList + 0x10);
        int teamID = driver.ReadMemory<int>(localPlayerPawn + offset.m_iTeamNum);
        Vector3 playerPositon = driver.ReadMemory<Vector3>(localPlayerPawn + offset.m_vOldOrigin);
        var currentViewMatrix = driver.ReadMemory<ViewMatrix>(client + offset.dwViewMatrix);

        for (int i = 0x78; i < 0xF00; i += 0x78) //loop 32, skip the first one -> localplayer
        {
            if (listEntry == nint.Zero)
                continue;

            nint currentController = driver.ReadMemory<nint>(listEntry + i);
            if (currentController == nint.Zero)
                continue;

            int pawnHandle = driver.ReadMemory<int>(currentController + offset.m_hPlayerPawn);
            if (pawnHandle == 0)
                break;


            nint entityList2 = driver.ReadMemory<nint>(entityList + 0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);
            nint currentPawn = driver.ReadMemory<nint>(entityList2 + 0x78 * (pawnHandle & 0x1FF));

            Entity entity = new Entity();
            entity.addres = currentPawn;
            entity.teamID = driver.ReadMemory<int>(entity.addres + offset.m_iTeamNum);

            if (entity.addres == localPlayerPawn)
                continue;

            nint observerServices = driver.ReadMemory<nint>(entity.addres + offset.m_pObserverServices);
            int observerTarget = driver.ReadMemory<int>(observerServices + offset.m_hObserverTarget);

            nint observerList = driver.ReadMemory<nint>(entityList + 0x8 * ((observerTarget & 0x7FFF) >> 9) + 0x10);
            nint targetPawn = driver.ReadMemory<nint>(observerList + 0x78 * (observerTarget & 0x1FF));

            if (targetPawn == localPlayerPawn)
                entity.isSpactating = true;

            if (entity.teamID == teamID && !settings.teamESP)
                continue;

            if (settings.radar)
                driver.WriteMemory<bool>(entity.addres + offset.m_entitySpottedState + offset.m_bSpotted, true);

            UpdateEntityPawn(entity, in currentViewMatrix);
            entity.distance = (float)Math.Round(Vector3.Distance(entity.position, playerPositon) / 39.37f); // from inch -> meters

            entity.addres = currentController;
            UpdateEntityControler(entity);
            playerPawnList.Add(entity);
        }
        return playerPawnList;
    }

    void UpdateEntityPawn(Entity entity, in ViewMatrix currentViewMatrix)
    {
        entity.healt = driver.ReadMemory<int>(entity.addres + offset.m_iHealth);
        entity.position = driver.ReadMemory<Vector3>(entity.addres + offset.m_vOldOrigin);
        entity.view = driver.ReadMemory<Vector3>(entity.addres + offset.m_vecViewOffset);
        entity.lifeState = driver.ReadMemory<uint>(entity.addres + offset.m_lifeState);
        entity.armor = driver.ReadMemory<int>(entity.addres + offset.m_ArmorValue);
        entity.spotted = driver.ReadMemory<bool>(entity.addres + offset.m_entitySpottedState + offset.m_bSpotted);

        nint sceneNode = driver.ReadMemory<nint>(entity.addres + offset.m_pGameSceneNode);
        nint boneMatrix = driver.ReadMemory<nint>(sceneNode + offset.m_modelState + 0x80);

        entity.abs = Vector3.Add(entity.position, new Vector3(0, 0, 75));

        entity.bones = ReadBones(boneMatrix);
        entity.bones2d = ReadBones2d(entity.bones, in currentViewMatrix);

        entity.orignScreenPosition = Vector2.Add(WorldToScreen(in currentViewMatrix, entity.position), windowLocation);
        entity.absScreenPosition = Vector2.Add(WorldToScreen(in currentViewMatrix, entity.abs), windowLocation);

        nint currentWeapon = driver.ReadMemory<nint>(entity.addres + offset.m_pClippingWeapon);
        short weaponDeffinitionIndex = driver.ReadMemory<short>(currentWeapon + offset.m_AttributeManager + offset.m_Item + offset.m_iItemDefinitionIndex);

        if (weaponDeffinitionIndex != -1)
        {
            entity.currentWeaponIndex = weaponDeffinitionIndex;
            entity.currentWeaponName = Enum.GetName(typeof(Weapon), weaponDeffinitionIndex) ?? string.Empty;
        }
    }
    unsafe void UpdateEntityControler(Entity entity)
    {
        //entity.playerName = driver.ReadMemory<string>(entity.addres + offset.m_iszPlayerName).AsSpan().TrimEnd('\0').ToString(); //fix string reads
        entity.teamColor = settings.coloredBox ? teamColor[driver.ReadMemory<int>(entity.addres + offset.m_iCompTeammateColor) + 1] : settings.boxColor;
    }

    //------------------------------------------------------ VISUALS ---------------------------------------------

    void DrawVisuals(Entity entity)
    {
        float thickness = Math.Clamp(10 / entity.distance, 0.65f, 1.2f);

        Vector2 boxs = Vector2.Add(entity.absScreenPosition, new Vector2((entity.orignScreenPosition.Y - entity.absScreenPosition.Y) * 0.3f, 0));
        Vector2 boxe = Vector2.Add(entity.orignScreenPosition, new Vector2(-(entity.orignScreenPosition.Y - entity.absScreenPosition.Y) * 0.3f, 0));

        float barPercent = entity.healt / 100f;
        Vector2 barHeight = new Vector2(0, barPercent * (entity.orignScreenPosition.Y - entity.absScreenPosition.Y));
        Vector2 barStart = Vector2.Add(boxe, new Vector2(-6 * thickness, 0));

        float armorPercent = entity.armor / 100f;
        Vector2 armorBarStart = Vector2.Add(boxe, new Vector2(0, 6.5f * thickness));

        Vector2 nameStart = Vector2.Add(entity.absScreenPosition, new Vector2(-entity.playerName.Length * 4, -16));

        float boxWidth = boxs.X - boxe.X;
        float boxHeight = boxe.Y - boxs.Y;

        if (settings.enableBox)
        {
            if (settings.fillBox)
            {
                overlay.SetColor(settings.boxFillColor.X, settings.boxFillColor.Y, settings.boxFillColor.Z, settings.boxFillColor.W);
                overlay.FillRect(boxe.X, boxs.Y, boxWidth, boxHeight);
            }

            if (settings.cornerBox)
            {
                overlay.SetColor(settings.boxOutlineColor.X, settings.boxOutlineColor.Y, settings.boxOutlineColor.Z);

                overlay.DrawLine(boxe.X, boxs.Y, boxe.X + (boxWidth / 4), boxs.Y, thickness * 2 + 1);
                overlay.DrawLine(boxe.X, boxs.Y, boxe.X, boxs.Y + (boxHeight / 4), thickness * 2 + 1);

                overlay.DrawLine(boxs.X, boxs.Y, boxs.X - (boxWidth / 4), boxs.Y, thickness * 2 + 1);
                overlay.DrawLine(boxs.X, boxs.Y, boxs.X, boxs.Y + (boxHeight / 4), thickness * 2 + 1);

                overlay.DrawLine(boxe.X, boxe.Y, boxe.X + (boxWidth / 4), boxe.Y, thickness * 2 + 1);
                overlay.DrawLine(boxe.X, boxe.Y, boxe.X, boxe.Y - (boxHeight / 4), thickness * 2 + 1);

                overlay.DrawLine(boxs.X, boxe.Y, boxs.X - (boxWidth / 4), boxe.Y, thickness * 2 + 1);
                overlay.DrawLine(boxs.X, boxe.Y, boxs.X, boxe.Y - (boxHeight / 4), thickness * 2 + 1);

                overlay.SetColor(entity.teamColor.X, entity.teamColor.Y, entity.teamColor.Z);

                overlay.DrawLine(boxe.X, boxs.Y, boxe.X + (boxWidth / 4), boxs.Y, thickness * 2);
                overlay.DrawLine(boxe.X, boxs.Y, boxe.X, boxs.Y + (boxHeight / 4), thickness * 2);

                overlay.DrawLine(boxs.X, boxs.Y, boxs.X - (boxWidth / 4), boxs.Y, thickness * 2);
                overlay.DrawLine(boxs.X, boxs.Y, boxs.X, boxs.Y + (boxHeight / 4), thickness * 2);

                overlay.DrawLine(boxe.X, boxe.Y, boxe.X + (boxWidth / 4), boxe.Y, thickness * 2);
                overlay.DrawLine(boxe.X, boxe.Y, boxe.X, boxe.Y - (boxHeight / 4), thickness * 2);

                overlay.DrawLine(boxs.X, boxe.Y, boxs.X - (boxWidth / 4), boxe.Y, thickness * 2);
                overlay.DrawLine(boxs.X, boxe.Y, boxs.X, boxe.Y - (boxHeight / 4), thickness * 2);
            }
            else
            {
                overlay.SetColor(settings.boxOutlineColor.X, settings.boxOutlineColor.Y, settings.boxOutlineColor.Z);
                overlay.DrawRect(boxe.X, boxs.Y, boxWidth, boxHeight, thickness);
            }
        }


        if (settings.hpBar)
        {
            overlay.SetColor(0f, 0f, 0f);
            overlay.FillRect(barStart.X - thickness, barStart.Y, 3 * thickness, -(entity.orignScreenPosition.Y - entity.absScreenPosition.Y));

            overlay.SetColor(1f - barPercent, barPercent, 0f);
            overlay.FillRect(barStart.X, barStart.Y, 2.5f * thickness, -barHeight.Y);
        }

        if (settings.playerName && !string.IsNullOrEmpty(entity.playerName)) // does not work
        {
            overlay.SetColor(0f, 0f, 0f);
            overlay.DrawText((int)(nameStart.X + thickness * 2), (int)(nameStart.Y + thickness * 2), entity.playerName);

            overlay.SetColor(1f, 1f, 1f);
            overlay.DrawText((int)nameStart.X, (int)nameStart.Y, entity.playerName);
        }

        if (settings.weaponESP && !string.IsNullOrEmpty(entity.currentWeaponName))
        {
            overlay.SetColor(0f, 0f, 0f);
            overlay.DrawText((int)(nameStart.X + thickness * 2), (int)(nameStart.Y + thickness * 20), entity.currentWeaponName);

            overlay.SetColor(0.8f, 0.8f, 0.8f);
            overlay.DrawText((int)nameStart.X, (int)(nameStart.Y + 18), entity.currentWeaponName);
        }

        if (settings.playerDistance)
        {
            string distStr = entity.distance < 10.0f ? $"{entity.distance:F1}m" : $"{entity.distance:F0}m";
            Vector2 distanceStart = Vector2.Add(entity.orignScreenPosition, new Vector2(-boxWidth / 2, thickness * 4));

            float fontSize = 20 - entity.distance / 4;
            if(fontSize < 8f) fontSize = 8f;
            TextFormat textFormat = new(new Factory(), "Arial", fontSize);

            overlay.SetColor(0f, 0f, 0f);
            overlay.DrawTextOutline((int)distanceStart.X, (int)distanceStart.Y, distStr, textFormat);
        }

        if (settings.skeletonESP && entity.bones2d.Count >= 12)
        {
            overlay.SetColor(settings.skeletonColor.X, settings.skeletonColor.Y, settings.skeletonColor.Z);

            overlay.DrawLine(entity.bones2d[1].X, entity.bones2d[1].Y, entity.bones2d[0].X, entity.bones2d[0].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[1].X, entity.bones2d[1].Y, entity.bones2d[2].X, entity.bones2d[2].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[1].X, entity.bones2d[1].Y, entity.bones2d[3].X, entity.bones2d[3].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[3].X, entity.bones2d[3].Y, entity.bones2d[4].X, entity.bones2d[4].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[4].X, entity.bones2d[4].Y, entity.bones2d[5].X, entity.bones2d[5].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[1].X, entity.bones2d[1].Y, entity.bones2d[6].X, entity.bones2d[6].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[6].X, entity.bones2d[6].Y, entity.bones2d[7].X, entity.bones2d[7].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[7].X, entity.bones2d[7].Y, entity.bones2d[8].X, entity.bones2d[8].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[0].X, entity.bones2d[0].Y, entity.bones2d[9].X, entity.bones2d[9].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[9].X, entity.bones2d[9].Y, entity.bones2d[10].X, entity.bones2d[10].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[0].X, entity.bones2d[0].Y, entity.bones2d[11].X, entity.bones2d[11].Y, thickness * 2);
            overlay.DrawLine(entity.bones2d[11].X, entity.bones2d[11].Y, entity.bones2d[12].X, entity.bones2d[12].Y, thickness * 2);
        }
    }

    bool IsPixelInsideScreen(Vector2 pixel, Vector2 pixel2)
    {
        return (pixel.X > windowLocation.X && pixel.X < windowLocation.X + windowSize.X && pixel.Y > windowLocation.Y && pixel.Y < windowSize.Y + windowLocation.Y)
            || (pixel2.X > windowLocation.X && pixel2.X < windowLocation.X + windowSize.X && pixel2.Y > windowLocation.Y && pixel2.Y < windowSize.Y + windowLocation.Y);
    }

    Vector2 WorldToScreen(in ViewMatrix matrix, Vector3 pos)
    {
        Vector2 screenCoordinates = new();

        float screenW = (matrix.m41 * pos.X) + (matrix.m42 * pos.Y) + (matrix.m43 * pos.Z) + matrix.m44;

        if (screenW > 0.001f)
        {
            float screenX = (matrix.m11 * pos.X) + (matrix.m12 * pos.Y) + (matrix.m13 * pos.Z) + matrix.m14;
            float screenY = (matrix.m21 * pos.X) + (matrix.m22 * pos.Y) + (matrix.m23 * pos.Z) + matrix.m24;

            float camX = windowSize.X / 2;
            float camY = windowSize.Y / 2;

            float X = camX + (camX * screenX / screenW);
            float Y = camY - (camY * screenY / screenW);

            screenCoordinates.X = X;
            screenCoordinates.Y = Y;
            return screenCoordinates;
        }
        else
            return new(-1, -1);
    }

    //------------------------------------------------------FEATURES - MISC------------------------------------------------

    void Mix()
    {
        nint forceDuck = client + offset.forceDuck;

        while (true){
            localPlayerPawn = driver.ReadMemory<nint>(client + offset.dwLocalPlayerPawn);
            localPlayerController = driver.ReadMemory<nint>(client + offset.dwLocalPlayerController);
            Vector3 velocity = driver.ReadMemory<Vector3>(localPlayerPawn + offset.m_vecAbsVelocity);

            float flashTime = driver.ReadMemory<float>(localPlayerPawn + offset.m_flFlashBangTime);
            uint fFlag = driver.ReadMemory<uint>(localPlayerPawn + offset.m_fFlags);

            if (flashTime > 0 && settings.antiFlash)
                driver.WriteMemory<float>(localPlayerPawn + offset.m_flFlashBangTime, 0);

            if (settings.autoDuck){
                int shootsFired = driver.ReadMemory<int>(localPlayerPawn + offset.m_iShotsFired);
                if (shootsFired >= 1 && previousShootsFired != shootsFired)
                {
                    driver.WriteMemory<int>(forceDuck, 65537);
                    previousShootsFired = shootsFired;
                    isCrouched = true;
                }
                else if (shootsFired == 0 && previousShootsFired >= 2)
                {
                    driver.WriteMemory<int>(forceDuck, 256);
                    isCrouched = false;
                }
            }

            if (settings.enableHitSound)
            {
                nint bulletServices = driver.ReadMemory<nint>(localPlayerPawn + offset.m_pBulletServices);
                int totalHits = driver.ReadMemory<int>(bulletServices + offset.m_totalHitsOnServer);

                if(totalHits != previousTotalHits)
                {
                    if(totalHits == 0 &&  previousTotalHits != 0)
                    {

                    }
                    else
                    {
                        PlaySound();
                    }
                }
                previousTotalHits = totalHits;
            }
            //BombTimer();
            //SmokeMNG();
        }
    }

    

    void PlaySound()
    {
        if (outputDevice != null)
        {
            outputDevice.Stop();
            outputDevice.Dispose();
            audioFile.Dispose();
        }

        audioFile = new AudioFileReader("Sounds/hitmarker.mp3");
        outputDevice = new WaveOutEvent();
        outputDevice.Init(audioFile);
        outputDevice.Volume = 0.3f;
        outputDevice.Play();
    }


    void BombTimer() //needs work
    {
        bool isBombPlanted = driver.ReadMemory<bool>(client + offset.dwPlantedC4 - 0x8);
        bool isBeingDefused = driver.ReadMemory<bool>(offset.dwPlantedC4 + offset.m_bBeingDefused);
        float defuseTime = driver.ReadMemory<float>(offset.dwPlantedC4 + offset.m_flDefuseCountDown);

        ulong time = CurrentTimeMillis();

        if (isBombPlanted && !isPlanted && (plantTime == 0 || time - plantTime > 60000))
        {
            isPlanted = true;
            plantTime = time;
        }

        float remaining = (40000f - (time - plantTime)) / 1000f;

        if (isPlanted && remaining >= 0)
            Console.WriteLine(getBombSite() + remaining);

        if (isPlanted && !isBombPlanted)
            isPlanted = false;
    }

    int getBombSite() // does not work
    {
        nint plantedPtr = driver.ReadMemory<nint>(client + offset.dwPlantedC4);
        if (plantedPtr == nint.Zero)
            return 0;

        nint bombEntity = driver.ReadMemory<nint>(plantedPtr + 0x0);
        if (bombEntity == nint.Zero)
            return 0;

        int site = driver.ReadMemory<int>(client + offset.dwPlantedC4 + offset.m_nBombSite);
        return site;
    }


    static ulong CurrentTimeMillis()
    {
        return (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    //------------------------------------------------------FEATURES - AIM-------------------------------------------------
    
    void Trigger(){
        while (Sleep(5000)){
            while (settings.enableTrigger){
                int team = driver.ReadMemory<int>(localPlayerPawn + offset.m_iTeamNum);
                int entIndex = driver.ReadMemory<int>(localPlayerPawn + offset.m_iIDEntIndex);

                if (entIndex != -1){
                    nint listEntry = driver.ReadMemory<nint>(entityList + 0x8 * ((entIndex & 0x7FFF) >> 9) + 0x10);
                    nint currentPawn = driver.ReadMemory<nint>(listEntry + 0x78 * (entIndex & 0x1FF));
                    int health = driver.ReadMemory<int>(currentPawn + offset.m_iHealth);
                    int entityTeam = driver.ReadMemory<int>(currentPawn + offset.m_iTeamNum);
                    Vector3 velocity = driver.ReadMemory<Vector3>(localPlayerPawn + offset.m_vecAbsVelocity);

                    if ((GetAsyncKeyState(settings.triggerHotkey) < 0 || !settings.enableHotKey) && (velocity.Z < 10f && velocity.Z > -10f) && !(settings.disableWhileStrafing && !(velocity.X < 5f && velocity.Y < 5f))){
                        if (team != (settings.teamTrigger ? 0 : entityTeam) && (entityTeam == 2 || entityTeam == 3) && health < 101 && health > 0){
                            if (settings.trggierDelay)
                                Thread.Sleep(settings.delay);
                            Driver.mouse_event(0x02 | 0x04, 0, 0, 0, nuint.Zero);
                            Thread.Sleep(1);
                        }
                    }
                }
            }
        }
    }

    void AimLockUpdate()
    {
        while (Sleep(5000))
        {
            while (settings.enableAim)
            {
                List<Entity> entities = new List<Entity>();

                nint listEntry = driver.ReadMemory<nint>(entityList + 0x10);
                localPlayer.teamID = driver.ReadMemory<int>(localPlayerPawn + offset.m_iTeamNum);

                var currentViewMatrix = driver.ReadMemory<ViewMatrix>(client + offset.dwViewMatrix);

                for (int i = 0x78; i < 0xF00; i += 0x78)
                {
                    if (listEntry == nint.Zero)
                        break;

                    nint currentController = driver.ReadMemory<nint>(listEntry + i);

                    if (currentController == nint.Zero)
                        continue;

                    int pawnHandle = driver.ReadMemory<int>(currentController + offset.m_hPlayerPawn);

                    nint entityList2 = driver.ReadMemory<nint>(entityList + 0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);
                    nint currentPawn = driver.ReadMemory<nint>(entityList2 + 0x78 * (pawnHandle & 0x1FF));
                    nint currentWeapon = driver.ReadMemory<nint>(currentPawn + offset.m_pClippingWeapon);

                    if (pawnHandle == 0)
                        continue;

                    int health = driver.ReadMemory<int>(currentPawn + offset.m_iHealth);
                    int team = driver.ReadMemory<int>(currentPawn + offset.m_iTeamNum);
                    short weaponDeffinitionIndex = driver.ReadMemory<short>(currentWeapon + offset.m_AttributeManager + offset.m_Item + offset.m_iItemDefinitionIndex);

                    if (team == localPlayer.teamID && !settings.teamAim)
                        continue;
                    if (health < 1 || weaponDeffinitionIndex == 0)
                        continue;

                    localPlayer.position = driver.ReadMemory<Vector3>(localPlayerPawn + offset.m_vOldOrigin);
                    localPlayer.view = driver.ReadMemory<Vector3>(localPlayerPawn + offset.m_vecViewOffset);

                    Entity entity = new Entity();

                    entity.addres = currentPawn;
                    if (entity.addres == localPlayerPawn)
                        continue;
                    entity.position = driver.ReadMemory<Vector3>(currentPawn + offset.m_vOldOrigin);
                    entity.distance = Vector3.Distance(entity.position, localPlayer.position);
                    entity.healt = driver.ReadMemory<int>(currentPawn + offset.m_iHealth);

                    nint sceneNode = driver.ReadMemory<nint>(currentPawn + offset.m_pGameSceneNode);
                    nint boneMatrix = driver.ReadMemory<nint>(sceneNode + offset.m_modelState + 0x80);

                    entity.head = driver.ReadMemory<Vector3>(boneMatrix + 6 * 32); //need fix

                    entity.head2D = WorldToScreen(in currentViewMatrix, entity.head);
                    entity.pixelDistance = Vector2.Distance(entity.head2D, new Vector2(windowSize.X / 2, windowSize.Y / 2));
                    entities.Add(entity);
                }
                AimLock(entities);
            }
        }
    }



    void AimLock(List<Entity> entities)
    {
        entities = entities.OrderBy(o => o.pixelDistance).ToList();
        if (GetAsyncKeyState(settings.aimHotkey) < 0)
            if (entities.Count > 0)
            {
                Vector3 playerView = Vector3.Add(localPlayer.position, localPlayer.view);
                Vector2 newAngle = CalculateAngles(playerView, entities[0].head);

                if (settings.enableAimFOV)
                {
                    if (entities[0].pixelDistance < settings.aimFOV)
                        driver.WriteMemory<Vector2>(client + offset.dwViewAngles, newAngle);
                }
                else
                    driver.WriteMemory<Vector2>(client + offset.dwViewAngles, newAngle);
            }
    }


    Vector2 CalculateAngles(Vector3 from, Vector3 to)
    {
        float deltaX = to.X - from.X;
        float deltaY = to.Y - from.Y;
        float yaw = (float)(Math.Atan2(deltaY, deltaX) * 180 / Math.PI);

        float deltaZ = to.Z - from.Z;
        double distance = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));
        float pitch = -(float)(Math.Atan2(deltaZ, distance) * 180 / Math.PI);

        return new(pitch, yaw);
    }

    List<Vector3> ReadBones(nint boneAddress)
    {
        List<Vector3> bones = new();
        foreach (var boneID in Enum.GetValues(typeof(BoneIds)))
            bones.Add(driver.ReadMemory<Vector3>(boneAddress + (int)boneID * 32));
        return bones;
    }

    List<Vector2> ReadBones2d(List<Vector3> bones, in ViewMatrix viewMatrix)
    {
        List<Vector2> bones2d = new();
        foreach (Vector3 bone in bones)
            bones2d.Add(WorldToScreen(in viewMatrix, bone));
        return bones2d;
    }

    //----------------------------------------------------------------------------------------------------------------------

    static void Main()
    {
        Console.WriteLine("Waiting for cs2 to load");
        while (true)
        {
            try
            {
                new Driver("cs2");
                break;
            }
            catch
            {
                Thread.Sleep(3000);
            }
        }
        Console.Clear();

        Program program = new();
        Thread mainThread = new(program.MainThread);
        mainThread.Start();
        mainThread.Join();
    }

    bool Sleep(int ms)
    {
        Thread.Sleep(ms);
        return true;
    }
}