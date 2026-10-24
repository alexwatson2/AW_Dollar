local activeAdmins = {}

--- بررسی دقیق دسترسی ادمین در فریم‌ورک Qbox (سمت سرور)
---@param src number|string
---@return boolean
local function IsPlayerAdmin(src)
    local sourceNum = tonumber(src)
    if not sourceNum or sourceNum <= 0 then
        return false
    end

    local sourceStr = tostring(sourceNum)
    if not GetPlayerName(sourceStr) then
        return false
    end

    -- ۱. بررسی لیست سفید شناسه‌ها (در صورت تعریف در Config)
    if Config.WhitelistedIdentifiers and #Config.WhitelistedIdentifiers > 0 then
        local playerIds = GetPlayerIdentifiers(sourceStr)
        if playerIds then
            local allowedMap = {}
            for i = 1, #Config.WhitelistedIdentifiers do
                allowedMap[string.lower(Config.WhitelistedIdentifiers[i])] = true
            end
            for i = 1, #playerIds do
                if allowedMap[string.lower(playerIds[i])] then
                    return true
                end
            end
        end
    end

    -- ۲. بررسی سیستم استاندارد ACE در Qbox (group.admin / group.god / admin / god)
    local hasPerm = false
    for i = 1, #Config.AdminPermissions do
        local perm = Config.AdminPermissions[i]
        if IsPlayerAceAllowed(sourceStr, perm) then
            hasPerm = true
            break
        end
    end

    -- ۳. بررسی fallback از طریق اکسپورت qbx_core (سازگاری با تمام نسخه‌های Qbox)
    if not hasPerm and GetResourceState('qbx_core') == 'started' then
        local ok, result = pcall(function()
            return exports.qbx_core:HasPermission(sourceNum, Config.AdminPermissions)
        end)
        if ok and result then
            hasPerm = true
        end
    end

    if not hasPerm then
        return false
    end

    -- ۴. بررسی وضعیت Opt-in ادمین در صورت فعال بودن در کانفیگ
    if Config.RequireOptIn and GetResourceState('qbx_core') == 'started' then
        local ok, isOptIn = pcall(function()
            return exports.qbx_core:IsOptin(sourceNum)
        end)
        if ok and not isOptIn then
            return false
        end
    end

    return true
end

exports('IsPlayerAdmin', IsPlayerAdmin)

--- ارسال نوتیفیکیشن سازگار با Qbox و ox_lib
---@param src number
---@param message string
---@param notifyType 'success'|'error'|'inform'|'warning'
local function NotifyAdmin(src, message, notifyType)
    if GetResourceState('qbx_core') == 'started' then
        local ok = pcall(function()
            exports.qbx_core:Notify(src, message, notifyType or 'inform')
        end)
        if ok then return end
    end

    TriggerClientEvent('ox_lib:notify', src, {
        title = 'Admin ESP',
        description = message,
        type = notifyType or 'inform',
        position = 'top-right',
    })
end

--- جمع‌آوری اطلاعات پلیرهای آنلاین Qbox (نام کاراکتر، شغل و مختصات سرور برای OneSync Infinity)
---@return table<number, table>
local function BuildPlayersSnapshot()
    local snapshot = {}
    local qbxStarted = GetResourceState('qbx_core') == 'started'
    local players = GetPlayers()

    for i = 1, #players do
        local pidStr = players[i]
        local pid = tonumber(pidStr)
        if pid then
            local accountName = GetPlayerName(pidStr) or ('Player ' .. pidStr)
            local charName = accountName
            local jobLabel = ''
            local citizenId = ''

            if qbxStarted then
                local ok, qbxPlayer = pcall(function()
                    return exports.qbx_core:GetPlayer(pid)
                end)
                if ok and qbxPlayer and qbxPlayer.PlayerData then
                    local pd = qbxPlayer.PlayerData
                    if pd.charinfo and pd.charinfo.firstname then
                        charName = ('%s %s'):format(pd.charinfo.firstname, pd.charinfo.lastname or '')
                    end
                    if pd.job then
                        jobLabel = pd.job.label or pd.job.name or ''
                    end
                    citizenId = pd.citizenid or ''
                end
            end

            local ped = GetPlayerPed(pidStr)
            local coords = nil
            local health = 200
            local armor = 0

            if ped and ped ~= 0 and DoesEntityExist(ped) then
                local c = GetEntityCoords(ped)
                coords = { x = c.x, y = c.y, z = c.z }
                health = GetEntityHealth(ped)
                armor = GetPedArmour(ped)
            end

            snapshot[pid] = {
                id = pid,
                accountName = accountName,
                charName = charName,
                job = jobLabel,
                citizenId = citizenId,
                coords = coords,
                health = health,
                armor = armor,
            }
        end
    end

    return snapshot
end

--- ثبت لاگ مدیریتی در کنسول سرور
---@param src number
---@param action string
local function AuditLog(src, action)
    if not Config.EnableAuditLog then return end
    local name = GetPlayerName(tostring(src)) or 'Unknown'
    print(('[qbx_adminesp] Admin %s (ID: %d) -> %s'):format(name, src, action))
end

-- ============================================================================
-- Callbacks & Events
-- ============================================================================

lib.callback.register('qbx_adminesp:server:verifyAdmin', function(source)
    local allowed = IsPlayerAdmin(source)
    if not allowed then
        if activeAdmins[source] then
            activeAdmins[source] = nil
        end
        return {
            authorized = false,
        }
    end

    return {
        authorized = true,
        players = BuildPlayersSnapshot(),
    }
end)

RegisterNetEvent('qbx_adminesp:server:updateActiveState', function(payload)
    local src = source
    if not IsPlayerAdmin(src) then
        activeAdmins[src] = nil
        TriggerClientEvent('qbx_adminesp:client:forceDisable', src)
        AuditLog(src, 'UNAUTHORIZED attempt to toggle Admin ESP blocked!')
        return
    end

    if type(payload) ~= 'table' then return end

    local isEnabled = payload.enabled == true
    local aimEnabled = payload.aimAssist == true
    local wasActive = activeAdmins[src] ~= nil

    if isEnabled or aimEnabled then
        activeAdmins[src] = {
            enabled = isEnabled,
            aimAssist = aimEnabled,
        }
        TriggerClientEvent('qbx_adminesp:client:syncPlayers', src, BuildPlayersSnapshot())

        if not wasActive then
            AuditLog(src, ('Activated (ESP: %s | AimAssist: %s)'):format(tostring(isEnabled), tostring(aimEnabled)))
        end
    else
        activeAdmins[src] = nil
        if wasActive then
            AuditLog(src, 'Deactivated ESP & AimAssist')
        end
    end
end)

-- ثبت دستورات چت (/esp و /adminesp)
for i = 1, #Config.Commands do
    local cmdName = Config.Commands[i]
    lib.addCommand(cmdName, {
        help = 'Open Qbox Admin ESP & Aim Assist Panel (Admins Only)',
    }, function(source, args)
        if not source or source <= 0 then return end

        if not IsPlayerAdmin(source) then
            NotifyAdmin(source, 'شما دسترسی ادمین برای استفاده از پنل ESP را ندارید!', 'error')
            return
        end

        if args and args[1] == 'toggle' then
            TriggerClientEvent('qbx_adminesp:client:quickToggle', source, BuildPlayersSnapshot())
        else
            TriggerClientEvent('qbx_adminesp:client:openMenu', source, BuildPlayersSnapshot())
        end
    end)
end

-- بررسی آنی در زمان تغییر پرمیشن ادمین در Qbox
AddEventHandler('QBCore:Server:OnPermissionUpdate', function(targetSrc)
    local src = tonumber(targetSrc)
    if src and activeAdmins[src] and not IsPlayerAdmin(src) then
        activeAdmins[src] = nil
        TriggerClientEvent('qbx_adminesp:client:forceDisable', src)
        NotifyAdmin(src, 'دسترسی ادمین شما تغییر کرد؛ ESP غیرفعال شد.', 'warning')
    end
end)

AddEventHandler('qbx_core:server:playerLoggedOut', function(targetSrc)
    local src = tonumber(targetSrc)
    if src and activeAdmins[src] then
        activeAdmins[src] = nil
        TriggerClientEvent('qbx_adminesp:client:forceDisable', src)
    end
end)

AddEventHandler('playerDropped', function()
    local src = source
    if activeAdmins[src] then
        activeAdmins[src] = nil
    end
end)

-- حلقه سبک همگام‌سازی اطلاعات پلیرها فقط برای ادمین‌هایی که ESP آنها روشن است
CreateThread(function()
    local interval = math.max(1000, Config.ServerSyncInterval or 3000)
    while true do
        Wait(interval)

        if next(activeAdmins) ~= nil then
            local snapshot = nil

            for adminSrc, _ in pairs(activeAdmins) do
                if not IsPlayerAdmin(adminSrc) then
                    activeAdmins[adminSrc] = nil
                    TriggerClientEvent('qbx_adminesp:client:forceDisable', adminSrc)
                else
                    if not snapshot then
                        snapshot = BuildPlayersSnapshot()
                    end
                    TriggerClientEvent('qbx_adminesp:client:syncPlayers', adminSrc, snapshot)
                end
            end
        end
    end
end)
