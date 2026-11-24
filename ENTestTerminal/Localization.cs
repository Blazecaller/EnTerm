namespace ENTestTerminal;

public enum AppLanguage
{
    Turkish,
    English,
}

public static class Loc
{
    public static AppLanguage Current { get; private set; } = AppLanguage.Turkish;

    public static void Toggle() => Current = Current == AppLanguage.Turkish ? AppLanguage.English : AppLanguage.Turkish;

    public static string T(string key) => Map.TryGetValue(key, out var v) ? (Current == AppLanguage.Turkish ? v.Tr : v.En) : key;

    // Firmware replies are always English; order matters ("Room PWM" before "PWM").
    private static readonly (System.Text.RegularExpressions.Regex Pattern, string Tr)[] DeviceReplies =
    {
        (new(@"^Room PWM(\d) Set: (.+)$"), "Oda Sıcaklığı PWM $1 ayarlandı: $2"),
        (new(@"^PWM(\d) Set: (.+)$"), "PWM$1 ayarlandı: $2"),
        (new(@"^Speed(\d) Set: (.+)$"), "Hız $1 ayarlandı: $2"),
        (new(@"^No Wind PWM Set: (.+)$"), "Rüzgarsız PWM ayarlandı: $1"),
        (new(@"^Heater Set Point: (.+)$"), "Isıtıcı ayar noktası ayarlandı: $1"),
        (new(@"^K([PID]) Set: (\S+) \(not saved\)$"), "K$1 ayarlandı: $2 (kaydedilmedi)"),
        (new(@"^K([PID]) Set: (.+)$"), "K$1 ayarlandı: $2"),
        (new(@"^ERROR, INVALID ROOM PWM$"), "HATA: Geçersiz oda sıcaklığı PWM değeri"),
        (new(@"^ERROR, INVALID PWM$"), "HATA: Geçersiz PWM değeri"),
        (new(@"^ERROR, INVALID SPEED$"), "HATA: Geçersiz hız değeri"),
        (new(@"^ERROR, HEATER PWM IS 0$"), "HATA: Isıtıcı PWM değeri 0"),
        (new(@"^ERROR, INVALID SET POINT$"), "HATA: Geçersiz ayar noktası"),
        (new(@"^ERROR, INVALID GAIN$"), "HATA: Geçersiz kazanç değeri"),
        (new(@"^ERROR\b,?\s*(.*)$"), "HATA: $1"),
        (new(@"^OK$"), "Tamam"),
    };

    public static string DeviceReply(string message)
    {
        if (Current != AppLanguage.Turkish) return message;
        var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            foreach (var (pattern, tr) in DeviceReplies)
            {
                if (!pattern.IsMatch(line)) continue;
                lines[i] = pattern.Replace(line, tr).TrimEnd(' ', ':');
                break;
            }
        }
        return string.Join("\r\n", lines);
    }

    private static readonly Dictionary<string, (string Tr, string En)> Map = new()
    {
        ["conn.title"] = ("Bağlantı", "Connection"),
        ["conn.port"] = ("Port:", "Port:"),
        ["conn.refresh"] = ("Yenile", "Refresh"),
        ["conn.baud"] = ("Baud:", "Baud:"),
        ["conn.parity"] = ("Parite:", "Parity:"),
        ["conn.databits"] = ("Data Biti:", "Data bits:"),
        ["conn.stopbits"] = ("Stop Biti:", "Stop bits:"),
        ["conn.connect"] = ("Bağlan", "Connect"),
        ["conn.disconnect"] = ("Bağlantıyı Kes", "Disconnect"),
        ["conn.statusHelp"] = (
            "Bağlantı durumu:\n" +
            "• Kırmızı -> Bağlı değil.\n" +
            "• Yeşil -> Bağlı, cihaz normal yanıt veriyor.\n" +
            "• Turuncu -> Bağlı, ancak cihazdan yanıt gelmiyor. (baud yanlış veya güç yok).\n" +
            "   Uygulama otomatik olarak yeniden bağlanmayı deniyor.\n"+
            "<Blazecaller-2026> 52",
            "Connection status:\n" +
            "• Red — Disconnected.\n" +
            "• Green — Connected, device responding normally.\n" +
            "• Orange — Connected, but no signal from the device (wrong baud rate or power loss).\n" +
            "   The app is automatically retrying the handshake.\n"+
            "<Blazecaller-2026> 52"),

        ["parity.none"] = ("Yok", "None"),
        ["parity.odd"] = ("Tek", "Odd"),
        ["parity.even"] = ("Çift", "Even"),
        ["parity.mark"] = ("İşaret", "Mark"),
        ["parity.space"] = ("Boşluk", "Space"),

        ["basic.title"] = ("Komutlar", "Commands"),
        ["cfg.sensors"] = ("Sensör Yapılandırması", "Sensor Configuration"),
        ["cfg.device"] = ("Cihaz Ayarları", "Device Settings"),
        ["cfg.deviceBaud"] = ("Cihaz Baud (EEPROM):", "Device baud (EEPROM):"),
        ["cfg.sensor"] = ("Sensör", "Sensor"),
        ["cfg.state"] = ("Durum", "State"),
        ["cfg.pickOne"] = ("Gönderilecek ayar", "Setting to send"),
        ["cfg.pickSensor"] = ("Bir sensör seçin.", "Select a sensor."),
        ["cfg.pickState"] = ("AÇIK veya KAPALI seçin.", "Select ON or OFF."),
        ["cfg.pickSetting"] = ("Seri No veya Cihaz Baud seçin.", "Select Serial No or Device baud."),
        ["basic.on"] = ("AÇIK", "ON"),
        ["basic.off"] = ("KAPALI", "OFF"),
        ["basic.serialLabel"] = ("Seri No:", "Serial No:"),
        ["basic.setSerial"] = ("Seri No Ayarla", "Set Serial Number"),
        ["basic.setBaud"] = ("Baud Kaydet", "Save Baud Rate"),
        ["basic.showSensorList"] = ("Sensor Listesi", "Show Sensor List"),
        ["basic.systemStatus"] = ("Sistem Durumu", "System Status"),
        ["basic.help"] = ("Yardım", "Help"),
        ["basic.advancedShow"] = ("Terminal ▶", "Terminal ▶"),
        ["basic.advancedHide"] = ("Terminal ◀", "Terminal ◀"),

        ["sensor.digitalTemp"] = ("Dijital Sıcaklık", "Digital Temp"),
        ["sensor.solar"] = ("Güneş", "Solar"),
        ["sensor.soil"] = ("Toprak", "Soil"),
        ["sensor.rain"] = ("Yağmur", "Rain"),
        ["sensor.bgtWind"] = ("BGT Rüzgar", "BGT Wind"),

        ["nav.calibration"] = ("Kalibrasyon Modu →", "Calibration Mode →"),
        ["nav.back"] = ("← Normal Mod", "← Standard Mode"),

        ["grp.tempHumidity"] = ("Sıcaklık ve Nem", "Temperature & Humidity"),
        ["grp.windVibration"] = ("Rüzgar ve Titreşim", "Wind & Vibration"),
        ["grp.powerStatus"] = ("Güç ve Cihaz Durumu", "Power & Device Status"),
        ["grp.zeroSpeedPwm"] = ("Rüzgar PWM", "Wind PWM"),
        ["grp.live"] = ("Canlı Değerler", "Live Readings"),
        ["grp.calibSpeed"] = ("Rüzgar Hızları", "Wind Speeds"),
        ["grp.heater"] = ("Isıtıcı", "Heater"),
        ["grp.pid"] = ("PID Kazançları (geçici, kaydedilmez)", "PID Gains (temporary, not saved)"),
        ["grp.thresholds"] = ("Eşikler ve Zamanlama", "Thresholds & Timing"),
        ["grp.globalStatus"] = ("Ortam Durumu", "Ambient Status"),
        ["grp.sensorLinks"] = ("Haberleşme Durumu", "Communication Status"),

        ["tile.digitalTemp"] = ("Dijital Sıcaklık", "Digital Temp"),
        ["tile.sht20Temp"] = ("SHT20 Sıcaklık", "SHT20 Temp"),
        ["tile.sht20Humidity"] = ("SHT20 Nem", "SHT20 Humidity"),
        ["tile.windSpeed"] = ("Rüzgar Hızı", "Wind Speed"),
        ["tile.accelTotalG"] = ("Titreşim", "Vibration"),
        ["tile.windPwmOutput"] = ("Rüzgar PWM Çıkışı", "Wind PWM Output"),
        ["tile.batteryLevel"] = ("Batarya Seviyesi", "Battery Level"),
        ["tile.version"] = ("Yazılım Sürümü", "Firmware Version"),
        ["tile.zeroSpeedPwm1"] = ("PWM1", "PWM1"),
        ["tile.zeroSpeedPwm2"] = ("PWM2", "PWM2"),
        ["tile.zeroSpeedPwm3"] = ("PWM3", "PWM3"),
        ["tile.roomTempPwm1"] = ("Oda Sıcaklığı PWM 1", "Room-Temp PWM 1"),
        ["tile.roomTempPwm2"] = ("Oda Sıcaklığı PWM 2", "Room-Temp PWM 2"),
        ["tile.calibSpeed1"] = ("Hız 1", "Speed 1"),
        ["tile.calibSpeed2"] = ("Hız 2", "Speed 2"),
        ["tile.calibSpeed3"] = ("Hız 3", "Speed 3"),
        ["tile.ambientTemp"] = ("Ortam Sıcaklığı", "Ambient Temp"),
        ["tile.heaterTemp"] = ("Isıtıcı Sıcaklığı (filtreli)", "Heater Temp (filtered)"),
        ["tile.heaterPwmLive"] = ("PWM", "PWM"),
        ["tile.noWindPwm"] = ("Rüzgarsız PWM", "No-Wind PWM"),
        ["tile.heaterSetpoint"] = ("Isıtıcı Ayar Noktası", "Heater Set Point"),
        ["tile.kp"] = ("Kp", "Kp"),
        ["tile.ki"] = ("Ki", "Ki"),
        ["tile.kd"] = ("Kd", "Kd"),
        ["tile.windSpeedSetpoint"] = ("Rüzgar Hızı Set Değeri", "Wind Speed Setpoint"),
        ["tile.accelThreshold"] = ("Titreşim Eşiği", "Tilt Threshold"),
        ["tile.sleepInterval"] = ("Uyku Aralığı", "Sleep Interval"),

        ["tile.statusTemp"] = ("Sıcaklık", "Temperature"),
        ["tile.statusBattery"] = ("Batarya", "Battery"),
        ["tile.statusVibration"] = ("Titreşim", "Vibration"),
        ["tile.statusWind"] = ("Rüzgar", "Wind"),
        ["tile.statusDeltaTemp"] = ("Delta Sıcaklık", "Delta Temp"),

        ["tile.linkAmbientTemp"] = ("Ortam Sıcaklığı (SPI)", "Ambient Temp (SPI)"),
        ["tile.linkHeaterTemp"] = ("Isıtıcı Sıcaklığı (SPI)", "Heater Temp (SPI)"),
        ["tile.linkAccel"] = ("Titreşim (I2C)", "Tilt (I2C)"),
        ["tile.linkSht20"] = ("SHT20 (I2C)", "SHT20 (I2C)"),
        ["tile.linkDigitalTemp"] = ("Dijital Sıcaklık (ID5)", "Digital Temp (ID5)"),
        ["tile.linkSolar"] = ("Güneş (ID6)", "Solar (ID6)"),
        ["tile.linkSoil"] = ("Toprak (ID7)", "Soil (ID7)"),
        ["tile.linkRain"] = ("Yağmur (ID8)", "Rain (ID8)"),
        ["tile.linkWind"] = ("Anemometre (ID9)", "Anemometer (ID9)"),

        ["status.high"] = ("Yüksek", "HIGH"),
        ["status.low"] = ("Düşük", "LOW"),
        ["status.linkOk"] = ("Var", "OK"),

        ["unit.min"] = ("dk", "min"),

        ["status.disconnected"] = ("Bağlı Değil", "Disconnected"),
        ["status.connected"] = ("Bağlı", "Connected"),
        ["status.noSignal"] = ("Sinyal Yok", "No Signal"),
        ["status.mode"] = ("Mod", "Mode"),
        ["status.modeUnknown"] = ("Bilinmiyor", "Unknown"),
        ["status.modeData"] = ("Veri", "Data"),
        ["status.modeCommand"] = ("Komut", "Command"),
        ["status.test"] = ("Test", "Test"),
        ["status.on"] = ("Açık", "On"),
        ["status.off"] = ("Kapalı", "Off"),
        ["status.calib"] = ("Kalibrasyon", "Calibration"),
        ["status.frames"] = ("Veri Sayısı", "Frames"),

        ["popup.sensorList"] = ("Sensor Listesi", "Sensor List"),
        ["popup.status"] = ("Sistem Durumu", "System Status"),
        ["popup.help"] = ("Yardım", "Help"),
        ["popup.sensorControl"] = ("Sensor Kontrolü", "Sensor Control"),
        ["popup.serialNo"] = ("Seri No", "Serial No"),
        ["popup.baudRate"] = ("Baud Hızı", "Baud Rate"),
        ["msg.commError"] = ("Cihazdan geçerli bir yanıt alınamadı.", "Could not get a valid response from the device."),
        ["msg.noPort"] = ("Önce bir COM portu seçin.", "Select a COM port first."),
        ["msg.noPortTitle"] = ("Port Seçilmedi", "No port selected"),
        ["msg.invalidBaud"] = ("Baud hızı pozitif bir sayı olmalıdır.", "Baud rate must be a positive number."),
        ["msg.invalidBaudTitle"] = ("Geçersiz Baud Hızı", "Invalid baud rate"),
        ["msg.connFailedTitle"] = ("Bağlantı Başarısız", "Connection failed"),
        ["msg.invalidSerial"] = ("Seri numarası sayısal olmalıdır.", "Serial number must be numeric."),
        ["msg.invalidSerialTitle"] = ("Geçersiz Seri No", "Invalid serial number"),
        ["msg.notConnected"] = ("Önce cihaza bağlanın.", "Connect to the device first."),
        ["msg.commandModeFailed"] = ("AT komut moduna girilemedi.", "Could not enter AT command mode."),
        ["msg.calibTitle"] = ("Kalibrasyon", "Calibration"),
        ["msg.calibEnterFailed"] = ("Cihaz kalibrasyon paketini onaylamadı (AT+CALIB=1).", "The device did not confirm the calibration pack (AT+CALIB=1)."),
        ["msg.calibExitFailed"] = ("Test moduna geri dönülemedi (AT+T).", "Could not return to test mode (AT+T)."),
        ["calib.unsupported"] = ("Cihaz AT+CALIB komutunu desteklemiyor (eski yazılım). Test paketi değerleri gösteriliyor.", "Device does not support AT+CALIB (older firmware). Showing test pack values."),
        ["calib.unsupportedBanner"] = ("Bu cihazın yazılımı kalibrasyon modunu desteklemiyor. Test paketi değerleri gösteriliyor; kalibrasyon ayarları kullanılamaz.", "This device's firmware does not support calibration mode. Showing test pack values; calibration controls are unavailable."),
        ["calib.setZeroWind"] = ("Rüzgarı Sıfırla", "Set Zero Wind"),
        ["calib.advancedShow"] = ("Gelişmiş ▼", "Advanced ▼"),
        ["calib.advancedHide"] = ("Gelişmiş ▲", "Advanced ▲"),
        ["calib.endedByDevice"] = ("Kalibrasyon modu kapandı, normal moda dönülüyor.", "Calibration mode was switched off; returning to standard mode."),

        ["dlg.command"] = ("Komut", "Command"),
        ["dlg.current"] = ("Mevcut değer", "Current value"),
        ["dlg.range"] = ("İzin verilen aralık", "Allowed range"),
        ["dlg.integer"] = ("tam sayı", "integer"),
        ["dlg.decimal"] = ("ondalık, en fazla 6 basamak", "decimal, up to 6 decimals"),
        ["dlg.newValue"] = ("Yeni değer:", "New value:"),
        ["dlg.ok"] = ("Tamam", "OK"),
        ["dlg.send"] =("Gönder", "Send"),
        ["dlg.cancel"] = ("İptal", "Cancel"),
        ["dlg.invalid"] = ("Geçersiz değer. İzin verilen aralık: {0}", "Invalid value. Allowed range: {0}"),
        ["dlg.temporary"] = ("Kaydedilmez: cihaz yeniden başlatıldığında varsayılan değere döner.", "Not saved: reverts to the firmware default when the device resets."),
        ["dlg.nowPrompt"] = ("Rüzgarsız PWM, mevcut ısıtıcı PWM çıkışına ({0}) ayarlanacak.", "No-wind PWM will be set to the current heater PWM output ({0})."),
        ["dlg.nowNote"] = ("Isıtıcı PWM 0 ise cihaz komutu reddeder.", "The device rejects this if the heater PWM is 0."),
        ["dlg.notSaved"] = ("Not: bu değer EEPROM'a kaydedilmez.", "Note: this value is not saved to EEPROM."),

        ["msg.handshakeFailed"] = ("Komut moduna girilemedi, bağlantı açık kalıyor.", "Could not confirm command mode; connection stays open."),
    };
}
