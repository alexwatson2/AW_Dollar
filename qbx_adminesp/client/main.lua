AdminESP = {
    isAuthorized = false,
    menuOpen = false,
    settings = {},
    colors = {
        player = { r = 255, g = 0, b = 0 },
        npc = { r = 0, g = 255, b = 0 },
        fov = { r = 255, g = 255, b = 255 },
    },
    serverPlayers = {},
    trackedTargets = {},
    lockedTarget = nil,
}

local KVP_KEY = 'qbx_adminesp_settings_v1'

--- تبدیل کد رنگ هگز (#RRGGBB) به جدول RGB
---@param hex string
---@param defR number
---@param defG number
---@param defB number
---@return { r: number, g: number, b: number }
function AdminESP.HexToRGB(hex, defR, defG, defB)
    if type(hex) ~= 'string' then
        return { r = defR, g = defG, b = defB }
    end
    local clean = hex:gsub('#', '')
    if #clean == 3 then
        clean = clean:sub(1, 1):rep(2) .. clean:sub(2, 2):rep(2) .. clean:sub(3, 3):rep(2)
    end
    if #clean ~= 6 then
        return { r = defR, g = defG, b = defB }
    end
    local r = tonumber(clean:sub(1, 2), 16)
    local g = tonumber(clean:sub(3, 4), 16)
    local b = tonumber(clean:sub(5, 6), 16)
    if not r or not g or not b then
        return { r = defR, g = defG, b = defB }
    end
    return { r = r, g = g, b = b }
end

--- کپی عمیق جدول تنظیمات پیش‌فرض
local function CloneDefaults()
    local copy = {}
    for k, v in pairs(Config.DefaultSettings) do
        copy[k] = v
    end
    return copy
end

--- بروزرسانی کش رنگ‌های RGB برای استفاده سریع در رندر 0ms
function AdminESP.RefreshColors()
    AdminESP.colors.player = AdminESP.HexToRGB(AdminESP.settings.playerColor, 255, 0, 0)
    AdminESP.colors.npc = AdminESP.HexToRGB(AdminESP.settings.npcColor, 0, 255, 0)
    AdminESP.colors.fov = AdminESP.HexToRGB(AdminESP.settings.fovColor, 255, 255, 255)
end

--- بارگذاری تنظیمات ذخیره‌شده ادمین از حافظه KVP کلاینت
local function LoadSavedSettings()
    local base = CloneDefaults()
    local raw = GetResourceKvpString(KVP_KEY)
    if raw and raw ~= '' then
        local ok, decoded = pcall(json.decode, raw)
        if ok and type(decoded) == 'table' then
            for k, defaultVal in pairs(base) do
                if decoded[k] ~= nil and type(decoded[k]) == type(defaultVal) then
                    base[k] = decoded[k]
                end
            end
        end
    end
    -- همیشه هنگام ورود اولیه خاموش باشد تا فقط با تایید سرور روشن شود
    base.enabled = false
    AdminESP.settings = base
    AdminESP.RefreshColors()
end

--- ذخیره تنظیمات فعلی در KVP
local function SaveSettings()
    local toSave = {}
    for k, v in pairs(AdminESP.settings) do
        if k ~= 'enabled' then
            toSave[k] = v
        end
    end
    SetResourceKvp(KVP_KEY, json.encode(toSave))
end

--- ارسال وضعیت لایه شفاف (دایره FOV و نشانگر وضعیت) به NUI
function AdminESP.SyncOverlayNUI()
    local active = AdminESP.isAuthorized and AdminESP.settings.enabled == true
    SendNUIMessage({
        action = 'updateOverlay',
        active = active,
        showStatusHud = active and AdminESP.settings.showStatusHud == true,
        playerEsp = AdminESP.settings.playerEsp == true,
        playerColor = AdminESP.settings.playerColor,
        npcEsp = AdminESP.settings.npcEsp == true,
        npcColor = AdminESP.settings.npcColor,
        aimAssist = active and AdminESP.settings.aimAssist == true,
        showFov = active and AdminESP.settings.aimAssist == true and AdminESP.settings.showFov == true,
        fovRadius = tonumber(AdminESP.settings.fovRadius) or 120,
        fovColor = AdminESP.settings.fovColor or '#FFFFFF',
    })
end

--- هماهنگ‌سازی تمام تردهای ESP و Aim Assist بر اساس وضعیت فعلی
function AdminESP.RefreshRuntime()
    AdminESP.RefreshColors()
    AdminESP.SyncOverlayNUI()

    local shouldRunEsp = AdminESP.isAuthorized and AdminESP.settings.enabled == true
    if shouldRunEsp then
        if AdminESP.StartESPLoops then
            AdminESP.StartESPLoops()
        end
        if AdminESP.settings.aimAssist and AdminESP.StartAimAssistLoop then
            AdminESP.StartAimAssistLoop()
        elseif AdminESP.StopAimAssistLoop then
            AdminESP.StopAimAssistLoop()
        end
    else
        if AdminESP.StopESPLoops then
            AdminESP.StopESPLoops()
        end
        if AdminESP.StopAimAssistLoop then
            AdminESP.StopAimAssistLoop()
        end
    end
end

--- نمایش نوتیفیکیشن کلاینت
---@param msg string
---@param nType 'success'|'error'|'inform'|'warning'
function AdminESP.Notify(msg, nType)
    if lib and lib.notify then
        lib.notify({
            title = 'Admin ESP',
            description = msg,
            type = nType or 'inform',
            position = 'top-right',
        })
    end
end

--- باز کردن منوی NUI پس از تایید دسترسی ادمین سمت سرور
---@param preloadedPlayers? table
local function OpenAdminESPMenu(preloadedPlayers)
    if AdminESP.menuOpen then
        AdminESP.CloseMenu()
        return
    end

    if preloadedPlayers then
        AdminESP.isAuthorized = true
        AdminESP.serverPlayers = preloadedPlayers
    else
        local result = lib.callback.await('qbx_adminesp:server:verifyAdmin', false)
        if not result or not result.authorized then
            AdminESP.isAuthorized = false
            return
        end
        AdminESP.isAuthorized = true
        if result.players then
            AdminESP.serverPlayers = result.players
        end
    end

    AdminESP.menuOpen = true
    SetNuiFocus(true, true)
    SendNUIMessage({
        action = 'openMenu',
        settings = AdminESP.settings,
        defaults = Config.DefaultSettings,
    })
end

function AdminESP.CloseMenu()
    if not AdminESP.menuOpen then return end
    AdminESP.menuOpen = false
    SetNuiFocus(false, false)
    SendNUIMessage({
        action = 'closeMenu',
    })
end

--- خاموش کردن اجباری توسط سرور (در صورت سلب دسترسی ادمین)
local function ForceDisableAll()
    AdminESP.isAuthorized = false
    AdminESP.settings.enabled = false
    AdminESP.CloseMenu()
    AdminESP.RefreshRuntime()
end

-- ============================================================================
-- رویدادهای شبکه سمت کلاینت | Client Net Events
-- ============================================================================

RegisterNetEvent('qbx_adminesp:client:openMenu', function(playersSnapshot)
    OpenAdminESPMenu(playersSnapshot)
end)

RegisterNetEvent('qbx_adminesp:client:quickToggle', function(playersSnapshot)
    AdminESP.isAuthorized = true
    if playersSnapshot then
        AdminESP.serverPlayers = playersSnapshot
    end

    AdminESP.settings.enabled = not AdminESP.settings.enabled
    TriggerServerEvent('qbx_adminesp:server:updateActiveState', {
        enabled = AdminESP.settings.enabled,
        aimAssist = AdminESP.settings.enabled and AdminESP.settings.aimAssist or false,
    })

    AdminESP.RefreshRuntime()
    if AdminESP.settings.enabled then
        AdminESP.Notify('Admin ESP فعال شد', 'success')
    else
        AdminESP.Notify('Admin ESP غیرفعال شد', 'inform')
    end
end)

RegisterNetEvent('qbx_adminesp:client:syncPlayers', function(playersSnapshot)
    if not AdminESP.isAuthorized then return end
    if type(playersSnapshot) == 'table' then
        AdminESP.serverPlayers = playersSnapshot
    end
end)

RegisterNetEvent('qbx_adminesp:client:forceDisable', function()
    ForceDisableAll()
end)

-- ============================================================================
-- NUI Callbacks
-- ============================================================================

RegisterNUICallback('closeMenu', function(_, cb)
    AdminESP.CloseMenu()
    cb({ ok = true })
end)

RegisterNUICallback('updateSettings', function(data, cb)
    if not AdminESP.isAuthorized or type(data) ~= 'table' then
        cb({ ok = false })
        return
    end

    for k, defaultVal in pairs(Config.DefaultSettings) do
        if data[k] ~= nil then
            if type(defaultVal) == 'boolean' then
                AdminESP.settings[k] = data[k] == true
            elseif type(defaultVal) == 'number' then
                local num = tonumber(data[k])
                if num then
                    AdminESP.settings[k] = num
                end
            elseif type(defaultVal) == 'string' and type(data[k]) == 'string' then
                AdminESP.settings[k] = data[k]
            end
        end
    end

    SaveSettings()

    TriggerServerEvent('qbx_adminesp:server:updateActiveState', {
        enabled = AdminESP.settings.enabled,
        aimAssist = AdminESP.settings.enabled and AdminESP.settings.aimAssist or false,
    })

    AdminESP.RefreshRuntime()
    cb({ ok = true, settings = AdminESP.settings })
end)

RegisterNUICallback('resetDefaults', function(_, cb)
    if not AdminESP.isAuthorized then
        cb({ ok = false })
        return
    end

    local keepEnabled = AdminESP.settings.enabled
    AdminESP.settings = CloneDefaults()
    AdminESP.settings.enabled = keepEnabled

    SaveSettings()

    TriggerServerEvent('qbx_adminesp:server:updateActiveState', {
        enabled = AdminESP.settings.enabled,
        aimAssist = AdminESP.settings.enabled and AdminESP.settings.aimAssist or false,
    })

    AdminESP.RefreshRuntime()
    cb({ ok = true, settings = AdminESP.settings })
end)

-- ============================================================================
-- ثبت کلیدهای میانبر | Key Mappings
-- ============================================================================

CreateThread(function()
    LoadSavedSettings()
    Wait(500)
    AdminESP.SyncOverlayNUI()
end)

if Config.OpenMenuKey and Config.OpenMenuKey ~= '' then
    RegisterCommand('+qbx_adminesp_menu', function()
        OpenAdminESPMenu()
    end, false)
    RegisterCommand('-qbx_adminesp_menu', function() end, false)
    RegisterKeyMapping('+qbx_adminesp_menu', 'Admin ESP: Open Settings Panel', 'keyboard', Config.OpenMenuKey)
end

if Config.QuickToggleKey and Config.QuickToggleKey ~= '' then
    RegisterCommand('+qbx_adminesp_toggle', function()
        local result = lib.callback.await('qbx_adminesp:server:verifyAdmin', false)
        if not result or not result.authorized then return end
        TriggerEvent('qbx_adminesp:client:quickToggle', result.players)
    end, false)
    RegisterCommand('-qbx_adminesp_toggle', function() end, false)
    RegisterKeyMapping('+qbx_adminesp_toggle', 'Admin ESP: Quick Toggle ON/OFF', 'keyboard', Config.QuickToggleKey)
end

AddEventHandler('onResourceStop', function(resName)
    if resName ~= GetCurrentResourceName() then return end
    if AdminESP.menuOpen then
        SetNuiFocus(false, false)
    end
    if AdminESP.StopESPLoops then
        AdminESP.StopESPLoops()
    end
    if AdminESP.StopAimAssistLoop then
        AdminESP.StopAimAssistLoop()
    end
end)
