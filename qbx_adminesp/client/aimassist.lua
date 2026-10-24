local aimLoopRunning = false

local BONE_MAP = {
    head = 31086,
    neck = 39317,
    chest = 24818,
    pelvis = 11816,
}

--- نرمال‌سازی اختلاف زاویه در بازه [-180, 180] برای جلوگیری از پرش دوربین
---@param diff number
---@return number
local function NormalizeAngleDelta(diff)
    return (diff + 540.0) % 360.0 - 180.0
end

--- محاسبه نقطه مرکز دید دوربین در فضای ۳بعدی برای رسم خط قفل هدف
local function GetCrosshairWorldPoint()
    local camPos = GetGameplayCamCoord()
    local camRot = GetGameplayCamRot(2)
    local pitch = math.rad(camRot.x)
    local yaw = math.rad(camRot.z)
    local cosP = math.cos(pitch)
    local fwd = vector3(-math.sin(yaw) * cosP, math.cos(yaw) * cosP, math.sin(pitch))
    return camPos + fwd * 1.5
end

--- بررسی معتبر بودن هدف برای Aim Assist
local function IsValidAimCandidate(localPed, localCoords, entry, cfg, maxAimDist)
    local ped = entry.ped
    if not ped or ped == 0 or ped == localPed or not DoesEntityExist(ped) then
        return false
    end

    if entry.isPlayer and not cfg.aimTargetPlayers then
        return false
    end
    if (not entry.isPlayer) and not cfg.aimTargetNpcs then
        return false
    end

    if IsEntityDead(ped) or GetEntityHealth(ped) <= 100 then
        return false
    end

    local pedCoords = GetEntityCoords(ped)
    local dist = #(localCoords - pedCoords)
    if dist > maxAimDist then
        return false
    end

    if cfg.aimVisibleOnly and not HasEntityClearLosToEntity(localPed, ped, 17) then
        return false
    end

    return true
end

function AdminESP.StartAimAssistLoop()
    if aimLoopRunning then return end
    aimLoopRunning = true

    CreateThread(function()
        local currentStickyPed = nil

        while aimLoopRunning and AdminESP.isAuthorized and AdminESP.settings.enabled and AdminESP.settings.aimAssist do
            if AdminESP.menuOpen then
                AdminESP.lockedTarget = nil
                Wait(100)
            else
                local cfg = AdminESP.settings
                local playerId = PlayerId()
                local localPed = PlayerPedId()
                local localCoords = GetEntityCoords(localPed)

                local resX, resY = GetActiveScreenResolution()
                if resX <= 0 then resX = 1920 end
                if resY <= 0 then resY = 1080 end

                local fovRadius = tonumber(cfg.fovRadius) or 120.0
                local scaledFovPx = fovRadius * (resY / 1080.0)
                local maxAimDist = tonumber(cfg.aimMaxDistance) or 150.0
                local boneId = BONE_MAP[cfg.aimBone or 'head'] or 31086

                local bestPed = nil
                local bestBonePos = nil
                local bestScreenDist = scaledFovPx

                -- جمع‌آوری کاندیداها (حتی اگر تگ نمایشی Player/NPC در ESP خاموش باشد، اگر در Aim Assist روشن باشد بررسی می‌شود)
                local candidates = AdminESP.trackedTargets
                local fallbackPoolUsed = false
                if #candidates == 0 and (cfg.aimTargetPlayers or cfg.aimTargetNpcs) then
                    fallbackPoolUsed = true
                    candidates = {}
                    local peds = GetGamePool('CPed')
                    for i = 1, #peds do
                        local p = peds[i]
                        if p ~= localPed and DoesEntityExist(p) then
                            candidates[#candidates + 1] = {
                                ped = p,
                                isPlayer = IsPedAPlayer(p),
                            }
                        end
                    end
                end

                for i = 1, #candidates do
                    local entry = candidates[i]
                    if IsValidAimCandidate(localPed, localCoords, entry, cfg, maxAimDist) then
                        local bonePos = GetPedBoneCoords(entry.ped, boneId, 0.0, 0.0, 0.0)
                        local onScreen, sx, sy = GetScreenCoordFromWorldCoord(bonePos.x, bonePos.y, bonePos.z)

                        if onScreen then
                            local dxPx = (sx - 0.5) * resX
                            local dyPx = (sy - 0.5) * resY
                            local screenDist = math.sqrt(dxPx * dxPx + dyPx * dyPx)

                            -- چسبندگی ملایم به هدف فعلی برای جلوگیری از پرش بین دو هدف نزدیک
                            local effectiveDist = (entry.ped == currentStickyPed) and (screenDist * 0.85) or screenDist

                            if screenDist <= scaledFovPx and effectiveDist <= bestScreenDist then
                                bestScreenDist = effectiveDist
                                bestPed = entry.ped
                                bestBonePos = bonePos
                            end
                        end
                    end
                end

                AdminESP.lockedTarget = bestPed
                currentStickyPed = bestPed

                if bestPed and bestBonePos then
                    -- نمایش خط قفل روی هدف انتخاب شده داخل FOV
                    if cfg.aimTargetLine then
                        local centerWorld = GetCrosshairWorldPoint()
                        local fovCol = AdminESP.colors.fov
                        DrawLine(
                            centerWorld.x, centerWorld.y, centerWorld.z,
                            bestBonePos.x, bestBonePos.y, bestBonePos.z,
                            fovCol.r, fovCol.g, fovCol.b, 195
                        )
                    end

                    -- اعمال Aim Assist هنگام نشانه‌گیری (کلیک راست ماوس / Free Aim)
                    local isAiming = IsPlayerFreeAiming(playerId) or IsControlPressed(0, 25)
                    if isAiming and not IsPedDeadOrDying(localPed, true) then
                        local camPos = GetGameplayCamCoord()
                        local camRot = GetGameplayCamRot(2)
                        local dir = bestBonePos - camPos
                        local distXY = math.sqrt(dir.x * dir.x + dir.y * dir.y)

                        if distXY > 0.05 then
                            -- سازگار با Lua 5.4 (math.atan دو آرگومانی)
                            local desiredHeading = math.deg(math.atan(-dir.x, dir.y))
                            local desiredPitch = math.deg(math.atan(dir.z, distXY))

                            local deltaHeading = NormalizeAngleDelta(desiredHeading - camRot.z)
                            local deltaPitch = desiredPitch - camRot.x

                            local smooth = math.max(1.0, tonumber(cfg.aimSmooth) or 4.5)
                            local dt = GetFrameTime()
                            local factor = (smooth <= 1.05) and 1.0 or math.min(1.0, (dt * 60.0) / smooth)

                            local newRelHeading = GetGameplayCamRelativeHeading() + (deltaHeading * factor)
                            local newRelPitch = math.max(-89.0, math.min(89.0, GetGameplayCamRelativePitch() + (deltaPitch * factor)))

                            SetGameplayCamRelativeHeading(newRelHeading)
                            SetGameplayCamRelativePitch(newRelPitch, 1.0)
                        end
                    end
                end

                if fallbackPoolUsed then
                    Wait(10)
                else
                    Wait(0)
                end
            end
        end

        AdminESP.lockedTarget = nil
        aimLoopRunning = false
    end)
end

function AdminESP.StopAimAssistLoop()
    aimLoopRunning = false
    AdminESP.lockedTarget = nil
end
