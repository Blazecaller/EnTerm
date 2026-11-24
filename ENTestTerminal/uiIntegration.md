# UI Integration: Test Mode, Calibration Mode and Setter Commands

These are the firmware changes in `Drivers/User/Src/user_mbusDriver.c` that the UI needs to follow.

**Compatibility rule:** the test pack is byte-identical to older firmware. Only the new calibration pack and the setter commands are new, so a UI that handles them still works with old devices.

## 1. Mode flow

```
                                    ┌──AT+T──▶ TEST MODE        (test pack)
DATA MODE ──ATO, ATO──▶ AT COMMAND MODE
                                    └──AT+CALIB=1──▶ CALIBRATION MODE (calibration pack)
```

Test mode and calibration mode are both entered from AT command mode, and only one stream runs at a time.

| Step | Send | Reply | Stream afterwards |
|---|---|---|---|
| Enter AT mode | `ATO` (twice) | `+CONFIRM`, then `+COMMAND MODE` | none |
| Enter test mode | `AT+T` (when test mode is off) | `BEGIN TEST` | test pack (calibration is switched off) |
| Leave test mode | `AT+T` (when test mode is on) | `END TEST` | none |
| Enter calibration mode | `AT+CALIB=1` | `CALIB PACK ON` / ` OK` | calibration pack (test mode is switched off) |
| Leave calibration mode | `AT+CALIB=0` | `CALIB PACK OFF` / ` OK` | none |
| Leave AT mode | `+++` | `+DATA MODE` | none (calibration is switched off) |

- Going from calibration back to the test page: send `AT+CALIB=0`, then `AT+T`. Sending only `AT+T` also works, because `BEGIN TEST` switches calibration off.
- `AT+CALIB=1` turns test mode off. So after leaving calibration, the next `AT+T` gives `BEGIN TEST`, not `END TEST`.
- `AT+T` is a toggle, so the UI has to track whether test mode is on.
- `+++` does **not** switch test mode off. This is unchanged from older firmware. If the UI comes back to AT mode later, the test stream may still be running.

## 2. Older firmware fallback

Older firmware doesn't know `AT+CALIB`. It replies `ERROR` or nothing, and whatever stream was running keeps running.

- If `AT+CALIB=1` gets no `CALIB PACK ON` reply within the timeout, stay in (or enter) test mode and keep showing the values from the test pack.
- Show calibration controls as unavailable for that device.
- The setter commands in section 5 also won't exist on older firmware. Expect `ERROR` or no reply.

## 3. Test pack (unchanged from older firmware)

Format: `|f0,...,f17|\r\n`, 18 fields, no tag. Floats have 2 decimals. Ints 8–16 are zero-padded to at least 2 digits (`%.02d`).

| # | Field | Type | Firmware variable |
|---|---|---|---|
| 0 | Digital temperature | float | `RS485_digitalTemperature` |
| 1 | SHT20 temperature | float | `SHT20_measured_temperature` |
| 2 | Humidity | float | `sht20_humidity` |
| 3 | Battery level | float | `gBatteryLevel` |
| 4 | Wind speed | float | `gWindSpeed` |
| 5 | Wind speed set value | float | `pWindSpeedSetVal` |
| 6 | Accel total G | float | `gAccelTotalG` |
| 7 | Accel threshold | float | `pAccelThreshold` |
| 8 | Global status | int | `gGlobalStatus` |
| 9 | Zero-speed PWM 1 | int | `pWindZeroSpeedPWM_1` |
| 10 | Zero-speed PWM 2 | int | `pWindZeroSpeedPWM_2` |
| 11 | Zero-speed PWM 3 | int | `pWindZeroSpeedPWM_3` |
| 12 | Room-temp PWM 1 | int | `pWindRoomTempPWM_1` |
| 13 | Room-temp PWM 2 | int | `pWindRoomTempPWM_3` |
| 14 | Heater PWM output (live) | int | `gWindPwmOutput` |
| 15 | Sleep interval | int | `pDeviceSleepInterval` |
| 16 | Version | int | `gVersion` |
| 17 | Sensor link status | hex `0xNNN` | `modbus.SensorLinkStatus` |

## 4. Calibration pack (new)

Format: `|C,f0,...,f16|\r\n`, a `C` tag followed by 17 fields. Ints are not zero-padded.

**Detection:** a pack whose first field is `C` is a calibration pack. Anything else is a test pack.

| # | Field | Type | Firmware variable |
|---|---|---|---|
| tag | `C` | text | |
| 0 | SHT20 temperature (live) | float | `SHT20_measured_temperature` |
| 1 | Wind speed (live) | float | `gWindSpeed` |
| 2 | Heater PWM output (live) | int | `gWindPwmOutput` |
| 3 | Heater temperature, filtered (live) | float | `gHeaterTemperatureFiltered` |
| 4 | Zero-speed PWM 1 | int | `pWindZeroSpeedPWM_1` |
| 5 | Zero-speed PWM 2 | int | `pWindZeroSpeedPWM_2` |
| 6 | Zero-speed PWM 3 | int | `pWindZeroSpeedPWM_3` |
| 7 | Room-temp PWM 1 | int | `pWindRoomTempPWM_1` |
| 8 | Room-temp PWM 2 | int | `pWindRoomTempPWM_3` |
| 9 | Calibration speed 1 | float | `pWindCalibrationSpeed_1` |
| 10 | Calibration speed 2 | float | `pWindCalibrationSpeed_2` |
| 11 | Calibration speed 3 | float | `pWindCalibrationSpeed_3` |
| 12 | No-wind PWM | int | `pWindNoWind_PWM` |
| 13 | Heater set point | float | `memHeaterSetpoint` |
| 14 | Kp | float | `pid.dispKp` |
| 15 | Ki | float | `pid.dispKi` |
| 16 | Kd | float | `pid.dispKd` |

Example: `|C,26.00,0.00,364,31.52,500,600,740,0,0,3.00,4.00,6.00,350,30.00,20.00,0.20,0.00|`

## 5. Setter commands (new)

These work in AT command mode, with or without test or calibration mode on. Each command ends with CR, LF or nothing. Any other character after the value is rejected.

| Command | Sets | Range | Saved to EEPROM | OK reply | Error reply |
|---|---|---|---|---|---|
| `AT+CALP1=<n>` … `AT+CALP3=<n>` | Zero-speed PWM 1–3 | 0–1000 integer | yes | `PWM<i> Set: <n>` | `ERROR, INVALID PWM` |
| `AT+CALS1=<f>` … `AT+CALS3=<f>` | Calibration speed 1–3 | ≥ 0 | yes | `Speed<i> Set: <f>` | `ERROR, INVALID SPEED` |
| `AT+CALR1=<n>`, `AT+CALR2=<n>` | Room-temp PWM 1 / 2 | 0–999 integer | yes | `Room PWM<i> Set: <n>` | `ERROR, INVALID ROOM PWM` |
| `AT+NOW` | No-wind PWM = current heater PWM (calibration field 2) | heater PWM must be > 0 | yes | `No Wind PWM Set: <n>` | `ERROR, HEATER PWM IS 0` |
| `AT+HSP=<f>` | Heater set point | 0 < f < 100 | yes | `Heater Set Point: <f>` | `ERROR, INVALID SET POINT` |
| `AT+KP=<f>`, `AT+KI=<f>`, `AT+KD=<f>` | PID gain | ≥ 0 | **no** | `K<P/I/D> Set: <f> (not saved)` | `ERROR, INVALID GAIN` |

Every OK reply ends with `\r\n OK\r\n`.

Float input is an optional sign, up to 6 integer digits, and an optional `.` followed by up to 6 decimals (for example `3.25`). Exponents (`1e-3`) are not accepted.

## 6. UI checklist

- [ ] Test page: keep the existing 18-field parser as it is.
- [ ] Parser: route packs starting with `|C,` to the calibration parser and everything else to the test parser.
- [ ] Calibration page: needs AT command mode only. Send `AT+CALIB=1` on open and `AT+CALIB=0` on close.
- [ ] After `CALIB PACK ON`, treat test mode as off. After `BEGIN TEST` or `+++`, treat calibration mode as off.
- [ ] Older firmware: if there is no `CALIB PACK ON` within the timeout, stay on the test pack, keep showing its values, and disable the calibration controls.
- [ ] Add inputs and buttons for `CALP1–3`, `CALS1–3`, `CALR1–2`, `HSP`, `KP/KI/KD`, and a "capture no-wind" button for `AT+NOW`.
- [ ] Read the setter replies: confirm on `OK`, and show the message on `ERROR`.
- [ ] Mark the PID gains as temporary. They go back to the firmware defaults on reset.
- [ ] Show `CALR2` as "Room PWM 2". It maps to `pWindRoomTempPWM_3` in firmware (its pair is zero-speed PWM 3).

## 7. Known limitations

- Gains below 0.01 are applied correctly but show as `0.00` in the pack and replies, because floats print with 2 decimals.
- `AT?` help text does not list the new commands.
