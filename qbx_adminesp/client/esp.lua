local scannerRunning = false
local renderRunning = false
local farPlayersList = {}
local createdBlips = {}

-- جفت استخوان‌های اسکلت کاراکتر در GTA V
local SKELETON_BONES = {
    { 31086, 39317 }, -- Head -> Neck
    { 39317, 24818 }, -- Neck -> Spine3
    { 24818, 24816 }, -- Spine3 -> Spine1
    { 24816, 11816 }, -- Spine1 -> Pelvis

    -- Left Arm
    { 39317, 45509 }, -- Neck -> L_UpperArm
    { 45509, 61163 }, -- L_UpperArm -> L_Forearm
    { 61163, 18905 }, -- L_Forearm -> L_Hand

    -- Right Arm
    { 39317, 40269 }, -- Neck -> R_UpperArm
    { 40269, 28252 }, -- R_UpperArm -> R_Forearm
    { 28252, 57005 }, -- R_Forearm -> R_Hand

    -- Left Leg
    { 11816, 58271 }, -- Pelvis -> L_Thigh
    { 58271, 63931 }, -- L_Thigh -> L_Calf
    { 63931, 14201 }, -- L_Calf -> L_Foot

    -- Right Leg
    { 11816, 51826 }, -- Pelvis -> R_Thigh
    { 51826, 36864 }, -- R_Thigh -> R_Calf
    { 36864, 52301 }, -- R_Calf -> R_Foot
}

--- پاکسازی کامل بلیپ‌های ساخته شده روی مینی‌مپ
local function ClearAllAdminBlips()
    for ped, blip in pairs(createdBlips) do
        if DoesBlipExist(blip) then
            RemoveBlip(blip)
        end
        createdBlips[ped] = nil
    end
end

--- محاسبه خون واقعی در GTA V (حذف پایه ۱۰۰ واحدی موتور بازی)
---@param ped number
---@return number hp, number maxHp, number ratio, boolean isDead
local function GetPedNormalizedHealth(ped)
    local rawHp = GetEntityHealth(ped)
    local rawMax = GetEntityMaxHealth(ped)
    local isDead = IsEntityDead(ped) or rawHp <= 100

    if isDead then
        return 0, 100, 0.0, true
    end

    local hp = rawHp
    local maxHp = rawMax
    if rawMax > 100 then
        hp = math.max(0, rawHp - 100)
        maxHp = math.max(1, rawMax - 100)
    else
        hp = math.max(0, rawHp)
        maxHp = math.max(1, rawMax)
    end

    local ratio = math.min(1.0, math.max(0.0, hp / maxHp))
    return hp, maxHp, ratio, false
end

--- دریافت نام نمایشی سلاح فعلی کاراکتر
---@param ped number
---@return string
local function GetPedWeaponLabel(ped)
    local weaponHash = GetSelectedPedWeapon(ped)
    if not weaponHash or weaponHash == `WEAPON_UNARMED` then
        return ''
    end
    return Config.WeaponNames[weaponHash] or 'Armed'
end

--- به‌روزرسانی بلیپ‌های رادار برای اهداف ردیابی‌شده
---@param activeSet table<number, table>
local function UpdateAdminBlips(activeSet)
    if not AdminESP.settings.adminBlips then
        if next(createdBlips) ~= nil then
            ClearAllAdminBlips()
        end
        return
    end

    -- حذف بلیپ‌های قدیمی
    for ped, blip in pairs(createdBlips) do
        if not activeSet[ped] or not DoesEntityExist(ped) then
            if DoesBlipExist(blip) then
                RemoveBlip(blip)
            end
            createdBlips[ped] = nil
        end
    end

    -- ایجاد یا آپدیت بلیپ‌های فعلی
    for ped, info in pairs(activeSet) do
        local blip = createdBlips[ped]
        if not blip or not DoesBlipExist(blip) then
            blip = AddBlipForEntity(ped)
            SetBlipSprite(blip, 1)
            SetBlipScale(blip, info.isPlayer and 0.85 or 0.68)
            SetBlipAsShortRange(blip, false)
            ShowHeadingIndicatorOnBlip(blip, true)
            SetBlipCategory(blip, info.isPlayer and 7 or 1)
            createdBlips[ped] = blip
        end

        SetBlipColour(blip, info.isPlayer and 1 or 2)
        SetBlipRotation(blip, math.ceil(GetEntityHeading(ped)))

        BeginTextCommandSetBlipName('STRING')
        AddTextComponentSubstringPlayerName(info.label or (info.isPlayer and 'Player' or 'NPC'))
        EndTextCommandSetBlipName(blip)
    end
end

-- ============================================================================
-- توابع کمکی رسم ۲بعدی و ۳بعدی در فضای صفحه
-- ============================================================================

--- رسم متن دو بعدی تمیز با حاشیه مشکی
local function DrawScreenText(x, y, scale, text, r, g, b, a)
    SetTextFont(4)
    SetTextProportional(true)
    SetTextScale(scale, scale)
    SetTextColour(r, g, b, a or 245)
    SetTextDropshadow(0, 0, 0, 0, 255)
    SetTextEdge(1, 0, 0, 0, 255)
    SetTextOutline()
    SetTextCentre(true)
    BeginTextCommandDisplayText('STRING')
    AddTextComponentSubstringPlayerName(text)
    EndTextCommandDisplayText(x, y)
end

--- رسم کادر کامل ۲بعدی (Full 2D Box) با حاشیه تیره
local function Draw2DBox(cx, cy, w, h, pxX, pxY, r, g, b, fill)
    if fill then
        DrawRect(cx, cy, w, h, r, g, b, 30)
    end

    local thickX = pxX * 1.5
    local thickY = pxY * 1.5
    local outX = pxX * 3.0
    local outY = pxY * 3.0

    local topY = cy - h * 0.5
    local botY = cy + h * 0.5
    local leftX = cx - w * 0.5
    local rightX = cx + w * 0.5

    -- حاشیه تیره بیرونی برای خوانایی روی پس‌زمینه‌های روشن
    DrawRect(cx, topY, w + outX, outY, 0, 0, 0, 175)
    DrawRect(cx, botY, w + outX, outY, 0, 0, 0, 175)
    DrawRect(leftX, cy, outX, h + outY, 0, 0, 0, 175)
    DrawRect(rightX, cy, outX, h + outY, 0, 0, 0, 175)

    -- کادر اصلی رنگی
    DrawRect(cx, topY, w, thickY, r, g, b, 240)
    DrawRect(cx, botY, w, thickY, r, g, b, 240)
    DrawRect(leftX, cy, thickX, h, r, g, b, 240)
    DrawRect(rightX, cy, thickX, h, r, g, b, 240)
end

--- رسم کادر گوشه‌ای مدرن (Corner Box)
local function DrawCornerBox(cx, cy, w, h, pxX, pxY, r, g, b, fill)
    if fill then
        DrawRect(cx, cy, w, h, r, g, b, 28)
    end

    local cornerW = w * 0.28
    local cornerH = h * 0.25
    local thickX = pxX * 1.8
    local thickY = pxY * 1.8

    local leftX = cx - w * 0.5
    local rightX = cx + w * 0.5
    local topY = cy - h * 0.5
    local botY = cy + h * 0.5

    -- پس‌زمینه تیره گوشه‌ها
    local bgX = pxX * 3.2
    local bgY = pxY * 3.2
    DrawRect(leftX + cornerW * 0.5, topY, cornerW + bgX, bgY, 0, 0, 0, 175)
    DrawRect(leftX, topY + cornerH * 0.5, bgX, cornerH + bgY, 0, 0, 0, 175)
    DrawRect(rightX - cornerW * 0.5, topY, cornerW + bgX, bgY, 0, 0, 0, 175)
    DrawRect(rightX, topY + cornerH * 0.5, bgX, cornerH + bgY, 0, 0, 0, 175)
    DrawRect(leftX + cornerW * 0.5, botY, cornerW + bgX, bgY, 0, 0, 0, 175)
    DrawRect(leftX, botY - cornerH * 0.5, bgX, cornerH + bgY, 0, 0, 0, 175)
    DrawRect(rightX - cornerW * 0.5, botY, cornerW + bgX, bgY, 0, 0, 0, 175)
    DrawRect(rightX, botY - cornerH * 0.5, bgX, cornerH + bgY, 0, 0, 0, 175)

    -- گوشه بالا چپ
    DrawRect(leftX + cornerW * 0.5, topY, cornerW, thickY, r, g, b, 245)
    DrawRect(leftX, topY + cornerH * 0.5, thickX, cornerH, r, g, b, 245)

    -- گوشه بالا راست
    DrawRect(rightX - cornerW * 0.5, topY, cornerW, thickY, r, g, b, 245)
    DrawRect(rightX, topY + cornerH * 0.5, thickX, cornerH, r, g, b, 245)

    -- گوشه پایین چپ
    DrawRect(leftX + cornerW * 0.5, botY, cornerW, thickY, r, g, b, 245)
    DrawRect(leftX, botY - cornerH * 0.5, thickX, cornerH, r, g, b, 245)

    -- گوشه پایین راست
    DrawRect(rightX - cornerW * 0.5, botY, cornerW, thickY, r, g, b, 245)
    DrawRect(rightX, botY - cornerH * 0.5, thickX, cornerH, r, g, b, 245)
end

--- رسم کادر سه‌بعدی چرخان دور کاراکتر (3D Bounding Box)
local function Draw3DPedBox(ped, pedCoords, r, g, b)
    local fwd = GetEntityForwardVector(ped)
    local right = vector3(fwd.y, -fwd.x, 0.0)

    local halfW = 0.42
    local halfD = 0.36
    local topZ = pedCoords.z + 0.92
    local botZ = pedCoords.z - 1.02

    local fX, fY = fwd.x * halfD, fwd.y * halfD
    local rX, rY = right.x * halfW, right.y * halfW

    local t1 = vector3(pedCoords.x + fX + rX, pedCoords.y + fY + rY, topZ)
    local t2 = vector3(pedCoords.x + fX - rX, pedCoords.y + fY - rY, topZ)
    local t3 = vector3(pedCoords.x - fX - rX, pedCoords.y - fY - rY, topZ)
    local t4 = vector3(pedCoords.x - fX + rX, pedCoords.y - fY + rY, topZ)

    local b1 = vector3(t1.x, t1.y, botZ)
    local b2 = vector3(t2.x, t2.y, botZ)
    local b3 = vector3(t3.x, t3.y, botZ)
    local b4 = vector3(t4.x, t4.y, botZ)

    -- سقف کادر
    DrawLine(t1.x, t1.y, t1.z, t2.x, t2.y, t2.z, r, g, b, 235)
    DrawLine(t2.x, t2.y, t2.z, t3.x, t3.y, t3.z, r, g, b, 235)
    DrawLine(t3.x, t3.y, t3.z, t4.x, t4.y, t4.z, r, g, b, 235)
    DrawLine(t4.x, t4.y, t4.z, t1.x, t1.y, t1.z, r, g, b, 235)

    -- کف کادر
    DrawLine(b1.x, b1.y, b1.z, b2.x, b2.y, b2.z, r, g, b, 235)
    DrawLine(b2.x, b2.y, b2.z, b3.x, b3.y, b3.z, r, g, b, 235)
    DrawLine(b3.x, b3.y, b3.z, b4.x, b4.y, b4.z, r, g, b, 235)
    DrawLine(b4.x, b4.y, b4.z, b1.x, b1.y, b1.z, r, g, b, 235)

    -- ستون‌های عمودی
    DrawLine(t1.x, t1.y, t1.z, b1.x, b1.y, b1.z, r, g, b, 235)
    DrawLine(t2.x, t2.y, t2.z, b2.x, b2.y, b2.z, r, g, b, 235)
    DrawLine(t3.x, t3.y, t3.z, b3.x, b3.y, b3.z, r, g, b, 235)
    DrawLine(t4.x, t4.y, t4.z, b4.x, b4.y, b4.z, r, g, b, 235)
end

--- رسم اسکلت استخوان‌بندی کاراکتر
local function DrawPedSkeleton(ped, r, g, b)
    for i = 1, #SKELETON_BONES do
        local pair = SKELETON_BONES[i]
        local b1 = GetPedBoneCoords(ped, pair[1], 0.0, 0.0, 0.0)
        local b2 = GetPedBoneCoords(ped, pair[2], 0.0, 0.0, 0.0)
        DrawLine(b1.x, b1.y, b1.z, b2.x, b2.y, b2.z, r, g, b, 220)
    end
end

--- رسم نوار عمودی خون (HP) و زره (Armor) کنار کادر
local function DrawHealthAndArmorBars(leftX, cy, h, pxX, pxY, hpRatio, armor)
    local barW = pxX * 3.0
    local bgW = barW + pxX * 2.0
    local hpX = leftX - pxX * 6.0

    -- پس‌زمینه تیره نوار خون
    DrawRect(hpX, cy, bgW, h + pxY * 2.0, 0, 0, 0, 190)

    if hpRatio > 0.0 then
        -- تغییر رنگ تدریجی سبز -> زرد -> قرمز بر اساس درصد خون
        local hr, hg
        if hpRatio > 0.5 then
            hr = math.floor((1.0 - hpRatio) * 2.0 * 255)
            hg = 235
        else
            hr = 245
            hg = math.floor(hpRatio * 2.0 * 220)
        end

        local fillH = h * hpRatio
        local fillY = (cy + h * 0.5) - fillH * 0.5
        DrawRect(hpX, fillY, barW, fillH, hr, hg, 45, 240)
    end

    -- نوار زره (Armor) در صورت وجود
    if armor and armor > 0 then
        local arRatio = math.min(1.0, armor / 100.0)
        local arX = hpX - pxX * 5.0
        local arH = h * arRatio
        local arY = (cy + h * 0.5) - arH * 0.5
        DrawRect(arX, cy, bgW, h + pxY * 2.0, 0, 0, 0, 190)
        DrawRect(arX, arY, barW, arH, 56, 189, 248, 240)
    end
end

--- محاسبه نقطه شروع خطوط Tracer بر اساس تنظیمات ادمین
local function GetTracerOriginWorld(localPed, originMode)
    if originMode == 'ped' then
        return GetEntityCoords(localPed)
    end

    local camPos = GetGameplayCamCoord()
    local camRot = GetGameplayCamRot(2)
    local pitch = math.rad(camRot.x)
    local yaw = math.rad(camRot.z)

    local cosP = math.cos(pitch)
    local fwd = vector3(-math.sin(yaw) * cosP, math.cos(yaw) * cosP, math.sin(pitch))

    if originMode == 'center' then
        return camPos + fwd * 1.5
    end

    -- حالت 'bottom': تبدیل دقیق پایین-وسط صفحه به نقطه ۳بعدی جلوی دوربین
    local up = vector3(math.sin(yaw) * math.sin(pitch), -math.cos(yaw) * math.sin(pitch), cosP)
    local halfFovRad = math.rad(GetGameplayCamFov() * 0.5)
    local dist = 1.5
    return camPos + fwd * dist - up * (dist * math.tan(halfFovRad) * 0.96)
end

-- ============================================================================
-- ترد اسکنر بهینه (هر ۲۰۰ میلی‌ثانیه) | Optimized Entity Scanner
-- ============================================================================

local function StartScannerThread()
    if scannerRunning then return end
    scannerRunning = true

    CreateThread(function()
        while scannerRunning and AdminESP.isAuthorized and AdminESP.settings.enabled do
            local cfg = AdminESP.settings
            local localPed = PlayerPedId()
            local localCoords = GetEntityCoords(localPed)
            local maxDist = tonumber(cfg.maxDistance) or 300.0

            -- نقشه سریع پدهای پلیرهای استریم شده
            local streamedPlayerByPed = {}
            local streamedServerIds = {}

            for _, playerIdx in ipairs(GetActivePlayers()) do
                local ped = GetPlayerPed(playerIdx)
                if ped and ped ~= 0 and ped ~= localPed and DoesEntityExist(ped) then
                    local serverId = GetPlayerServerId(playerIdx)
                    streamedPlayerByPed[ped] = {
                        playerIdx = playerIdx,
                        serverId = serverId,
                        playerName = GetPlayerName(playerIdx) or ('ID ' .. tostring(serverId)),
                    }
                    streamedServerIds[serverId] = true
                end
            end

            local newTargets = {}
            local blipSet = {}
            local peds = GetGamePool('CPed')

            for i = 1, #peds do
                local ped = peds[i]
                if ped ~= localPed and DoesEntityExist(ped) then
                    local pInfo = streamedPlayerByPed[ped]
                    local isPlayer = pInfo ~= nil

                    if (isPlayer and cfg.playerEsp) or (not isPlayer and cfg.npcEsp) then
                        local pedCoords = GetEntityCoords(ped)
                        local dist = #(localCoords - pedCoords)

                        if dist <= maxDist then
                            local hp, maxHp, hpRatio, isDead = GetPedNormalizedHealth(ped)
                            if not isDead or cfg.showDead then
                                local armor = GetPedArmour(ped)
                                local weaponLabel = cfg.weaponInfo and GetPedWeaponLabel(ped) or ''
                                local label = 'NPC'
                                local subJob = ''

                                if isPlayer then
                                    local srvData = AdminESP.serverPlayers[pInfo.serverId]
                                    if srvData and srvData.charName and srvData.charName ~= srvData.accountName then
                                        label = ('[%d] %s (%s)'):format(pInfo.serverId, srvData.charName, pInfo.playerName)
                                    else
                                        label = ('[%d] %s'):format(pInfo.serverId, pInfo.playerName)
                                    end
                                    if srvData and srvData.job and srvData.job ~= '' then
                                        subJob = srvData.job
                                    end
                                end

                                if isDead then
                                    label = label .. ' [DEAD]'
                                end

                                local entry = {
                                    ped = ped,
                                    isPlayer = isPlayer,
                                    serverId = isPlayer and pInfo.serverId or nil,
                                    label = label,
                                    job = subJob,
                                    weapon = weaponLabel,
                                    hp = hp,
                                    maxHp = maxHp,
                                    hpRatio = hpRatio,
                                    armor = armor,
                                    isDead = isDead,
                                    dist = dist,
                                }

                                newTargets[#newTargets + 1] = entry
                                blipSet[ped] = entry
                            end
                        end
                    end
                end
            end

            -- پلیرهای خارج از محدوده استریم کلاینت در سرورهای OneSync Infinity
            local newFar = {}
            local myServerId = GetPlayerServerId(PlayerId())
            if cfg.playerEsp and AdminESP.serverPlayers then
                for srvId, sData in pairs(AdminESP.serverPlayers) do
                    local numericId = tonumber(srvId)
                    if numericId and numericId ~= myServerId and not streamedServerIds[numericId] and sData.coords then
                        local farPos = vector3(sData.coords.x, sData.coords.y, sData.coords.z)
                        local dist = #(localCoords - farPos)
                        if dist <= maxDist then
                            local farLabel = ('[%d] %s'):format(numericId, sData.charName or sData.accountName or 'Player')
                            newFar[#newFar + 1] = {
                                serverId = numericId,
                                label = farLabel,
                                coords = farPos,
                                dist = dist,
                            }
                        end
                    end
                end
            end

            AdminESP.trackedTargets = newTargets
            farPlayersList = newFar
            UpdateAdminBlips(blipSet)

            Wait(200)
        end

        AdminESP.trackedTargets = {}
        farPlayersList = {}
        ClearAllAdminBlips()
        scannerRunning = false
    end)
end

-- ============================================================================
-- ترد رندر آنی (0ms فقط هنگام فعال بودن) | Smooth World-Locked Render Loop
-- ============================================================================

local function StartRenderThread()
    if renderRunning then return end
    renderRunning = true

    CreateThread(function()
        while renderRunning and AdminESP.isAuthorized and AdminESP.settings.enabled do
            local targets = AdminESP.trackedTargets
            local farTargets = farPlayersList
            local cfg = AdminESP.settings

            if #targets == 0 and #farTargets == 0 then
                Wait(100)
            else
                local localPed = PlayerPedId()
                local localCoords = GetEntityCoords(localPed)
                local resX, resY = GetActiveScreenResolution()
                if resX <= 0 then resX = 1920 end
                if resY <= 0 then resY = 1080 end
                local aspect = resX / resY
                local pxX = 1.0 / resX
                local pxY = 1.0 / resY

                local tracerStart = nil
                if cfg.tracers then
                    tracerStart = GetTracerOriginWorld(localPed, cfg.tracerOrigin or 'bottom')
                end

                local pCol = AdminESP.colors.player
                local nCol = AdminESP.colors.npc

                for i = 1, #targets do
                    local t = targets[i]
                    local ped = t.ped

                    if DoesEntityExist(ped) then
                        local col = t.isPlayer and pCol or nCol
                        local r, g, b = col.r, col.g, col.b

                        -- اگر Aim Assist روی این هدف قفل کرده باشد، خط اتصال اختصاصی نشان داده شود
                        local isLocked = (AdminESP.lockedTarget == ped)

                        local pedCoords = GetEntityCoords(ped)
                        local dist = #(localCoords - pedCoords)

                        -- ۱. رسم خط راهنما (Tracer)
                        if cfg.tracers and tracerStart then
                            DrawLine(
                                tracerStart.x, tracerStart.y, tracerStart.z,
                                pedCoords.x, pedCoords.y, pedCoords.z - 0.9,
                                r, g, b, 205
                            )
                        end

                        -- ۲. رسم اسکلت استخوان‌بندی (تا فاصله ۱۳۰ متر برای حفظ بالاترین فریم‌ریت)
                        if cfg.skeleton and dist <= 130.0 then
                            DrawPedSkeleton(ped, r, g, b)
                        end

                        -- ۳. رسم کادر ۳بعدی در صورت انتخاب استایل 3d
                        if cfg.boxEsp and cfg.boxStyle == '3d' then
                            Draw3DPedBox(ped, pedCoords, r, g, b)
                        end

                        -- محاسبه مختصات سر و پا روی صفحه برای کادر ۲بعدی، نوار خون و متن‌ها
                        local headWorld = GetPedBoneCoords(ped, 31086, 0.0, 0.0, 0.22)
                        local lFoot = GetPedBoneCoords(ped, 14201, 0.0, 0.0, -0.10)
                        local rFoot = GetPedBoneCoords(ped, 52301, 0.0, 0.0, -0.10)
                        local footZ = math.min(lFoot.z, rFoot.z, pedCoords.z - 0.98)
                        local footWorld = vector3(pedCoords.x, pedCoords.y, footZ)

                        local okTop, topX, topY = GetScreenCoordFromWorldCoord(headWorld.x, headWorld.y, headWorld.z)
                        local okBot, botX, botY = GetScreenCoordFromWorldCoord(footWorld.x, footWorld.y, footWorld.z)

                        if okTop and okBot then
                            local boxH = math.max(0.016, math.min(0.88, math.abs(botY - topY)))
                            local boxW = (boxH * 0.56) / aspect
                            local cx = (topX + botX) * 0.5
                            local cy = (topY + botY) * 0.5

                            -- ۴. رسم کادر ۲بعدی (Corner یا Full 2D)
                            if cfg.boxEsp and cfg.boxStyle ~= '3d' then
                                local drawR, drawG, drawB = r, g, b
                                if isLocked then
                                    drawR, drawG, drawB = 255, 230, 60
                                end

                                if cfg.boxStyle == '2d' then
                                    Draw2DBox(cx, cy, boxW, boxH, pxX, pxY, drawR, drawG, drawB, cfg.boxFill)
                                else
                                    DrawCornerBox(cx, cy, boxW, boxH, pxX, pxY, drawR, drawG, drawB, cfg.boxFill)
                                end
                            elseif cfg.boxFill and cfg.boxStyle == '3d' then
                                DrawRect(cx, cy, boxW, boxH, r, g, b, 24)
                            end

                            -- ۵. نقطه مشخص‌کننده سر (Head Dot)
                            if cfg.headDot and dist <= 160.0 then
                                local headExact = GetPedBoneCoords(ped, 31086, 0.0, 0.0, 0.0)
                                local okH, hx, hy = GetScreenCoordFromWorldCoord(headExact.x, headExact.y, headExact.z)
                                if okH then
                                    local dotW = pxX * 5.0
                                    local dotH = pxY * 5.0
                                    DrawRect(hx, hy, dotW + pxX * 2.0, dotH + pxY * 2.0, 0, 0, 0, 200)
                                    DrawRect(hx, hy, dotW, dotH, r, g, b, 250)
                                end
                            end

                            -- ۶. نوار خون (HP) و زره (Armor)
                            if cfg.healthBar then
                                local _, _, liveRatio, liveDead = GetPedNormalizedHealth(ped)
                                local liveArmor = GetPedArmour(ped)
                                DrawHealthAndArmorBars(cx - boxW * 0.5, cy, boxH, pxX, pxY, liveDead and 0.0 or liveRatio, liveArmor)
                            end

                            -- ۷. نمایش نام کاراکتر / پلیر / NPC بالای کادر
                            local textScale = math.max(0.24, math.min(0.34, 0.34 - (dist / 900.0)))
                            if cfg.names then
                                DrawScreenText(cx, cy - boxH * 0.5 - 0.022, textScale, t.label, r, g, b, 250)
                            end

                            -- ۸. نمایش فاصله و نام سلاح پایین کادر
                            local bottomParts = {}
                            if cfg.distance then
                                bottomParts[#bottomParts + 1] = ('%dm'):format(math.floor(dist + 0.5))
                            end
                            if cfg.weaponInfo and t.weapon and t.weapon ~= '' then
                                bottomParts[#bottomParts + 1] = t.weapon
                            end
                            if #bottomParts > 0 then
                                local bottomStr = table.concat(bottomParts, ' | ')
                                DrawScreenText(cx, cy + boxH * 0.5 + 0.004, textScale * 0.92, bottomStr, 235, 240, 245, 235)
                            end
                        end
                    end
                end

                -- رندر پلیرهای بسیار دور در OneSync Infinity (خارج از محدوده استریم کلاینت)
                for j = 1, #farTargets do
                    local ft = farTargets[j]
                    local okS, sx, sy = GetScreenCoordFromWorldCoord(ft.coords.x, ft.coords.y, ft.coords.z + 0.5)
                    if okS then
                        if cfg.tracers and tracerStart then
                            DrawLine(
                                tracerStart.x, tracerStart.y, tracerStart.z,
                                ft.coords.x, ft.coords.y, ft.coords.z,
                                pCol.r, pCol.g, pCol.b, 160
                            )
                        end
                        DrawRect(sx, sy, pxX * 7.0, pxY * 7.0, 0, 0, 0, 200)
                        DrawRect(sx, sy, pxX * 5.0, pxY * 5.0, pCol.r, pCol.g, pCol.b, 235)

                        local infoText = ft.label
                        if cfg.distance then
                            infoText = ('%s [%dm]'):format(ft.label, math.floor(ft.dist + 0.5))
                        end
                        DrawScreenText(sx, sy - 0.022, 0.26, infoText, pCol.r, pCol.g, pCol.b, 235)
                    end
                end

                Wait(0)
            end
        end

        renderRunning = false
    end)
end

function AdminESP.StartESPLoops()
    StartScannerThread()
    StartRenderThread()
end

function AdminESP.StopESPLoops()
    scannerRunning = false
    renderRunning = false
    AdminESP.trackedTargets = {}
    farPlayersList = {}
    ClearAllAdminBlips()
end
