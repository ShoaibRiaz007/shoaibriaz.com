namespace Portfolio.Application.Telemetry;

/// <summary>Maps an IANA time zone (what the browser reports) to the ISO 3166-1 alpha-2 country it belongs to.
/// Every zone in tzdata's zone.tab, plus the legacy aliases browsers still report (Chrome says "Asia/Calcutta",
/// "Europe/Kiev", ...). Embedded so country lookup needs no GeoIP database and no IP address.</summary>
public static class TimeZoneCountries
{
    // tzdata zone.tab: country code, then its zones.
    private const string ZoneTab = """
        AD Europe/Andorra
        AE Asia/Dubai
        AF Asia/Kabul
        AG America/Antigua
        AI America/Anguilla
        AL Europe/Tirane
        AM Asia/Yerevan
        AO Africa/Luanda
        AQ Antarctica/McMurdo Antarctica/Casey Antarctica/Davis Antarctica/DumontDUrville Antarctica/Mawson Antarctica/Palmer Antarctica/Rothera Antarctica/Syowa Antarctica/Troll Antarctica/Vostok
        AR America/Argentina/Buenos_Aires America/Argentina/Cordoba America/Argentina/Salta America/Argentina/Jujuy America/Argentina/Tucuman America/Argentina/Catamarca America/Argentina/La_Rioja America/Argentina/San_Juan America/Argentina/Mendoza America/Argentina/San_Luis America/Argentina/Rio_Gallegos America/Argentina/Ushuaia
        AS Pacific/Pago_Pago
        AT Europe/Vienna
        AU Australia/Lord_Howe Antarctica/Macquarie Australia/Hobart Australia/Melbourne Australia/Sydney Australia/Broken_Hill Australia/Brisbane Australia/Lindeman Australia/Adelaide Australia/Darwin Australia/Perth Australia/Eucla
        AW America/Aruba
        AX Europe/Mariehamn
        AZ Asia/Baku
        BA Europe/Sarajevo
        BB America/Barbados
        BD Asia/Dhaka
        BE Europe/Brussels
        BF Africa/Ouagadougou
        BG Europe/Sofia
        BH Asia/Bahrain
        BI Africa/Bujumbura
        BJ Africa/Porto-Novo
        BL America/St_Barthelemy
        BM Atlantic/Bermuda
        BN Asia/Brunei
        BO America/La_Paz
        BQ America/Kralendijk
        BR America/Noronha America/Belem America/Fortaleza America/Recife America/Araguaina America/Maceio America/Bahia America/Sao_Paulo America/Campo_Grande America/Cuiaba America/Santarem America/Porto_Velho America/Boa_Vista America/Manaus America/Eirunepe America/Rio_Branco
        BS America/Nassau
        BT Asia/Thimphu
        BW Africa/Gaborone
        BY Europe/Minsk
        BZ America/Belize
        CA America/St_Johns America/Halifax America/Glace_Bay America/Moncton America/Goose_Bay America/Blanc-Sablon America/Toronto America/Iqaluit America/Atikokan America/Winnipeg America/Resolute America/Rankin_Inlet America/Regina America/Swift_Current America/Edmonton America/Cambridge_Bay America/Inuvik America/Vancouver America/Creston America/Dawson_Creek America/Fort_Nelson America/Whitehorse America/Dawson
        CC Indian/Cocos
        CD Africa/Kinshasa Africa/Lubumbashi
        CF Africa/Bangui
        CG Africa/Brazzaville
        CH Europe/Zurich
        CI Africa/Abidjan
        CK Pacific/Rarotonga
        CL America/Santiago America/Coyhaique America/Punta_Arenas Pacific/Easter
        CM Africa/Douala
        CN Asia/Shanghai Asia/Urumqi
        CO America/Bogota
        CR America/Costa_Rica
        CU America/Havana
        CV Atlantic/Cape_Verde
        CW America/Curacao
        CX Indian/Christmas
        CY Asia/Nicosia Asia/Famagusta
        CZ Europe/Prague
        DE Europe/Berlin Europe/Busingen
        DJ Africa/Djibouti
        DK Europe/Copenhagen
        DM America/Dominica
        DO America/Santo_Domingo
        DZ Africa/Algiers
        EC America/Guayaquil Pacific/Galapagos
        EE Europe/Tallinn
        EG Africa/Cairo
        EH Africa/El_Aaiun
        ER Africa/Asmara
        ES Europe/Madrid Africa/Ceuta Atlantic/Canary
        ET Africa/Addis_Ababa
        FI Europe/Helsinki
        FJ Pacific/Fiji
        FK Atlantic/Stanley
        FM Pacific/Chuuk Pacific/Pohnpei Pacific/Kosrae
        FO Atlantic/Faroe
        FR Europe/Paris
        GA Africa/Libreville
        GB Europe/London
        GD America/Grenada
        GE Asia/Tbilisi
        GF America/Cayenne
        GG Europe/Guernsey
        GH Africa/Accra
        GI Europe/Gibraltar
        GL America/Nuuk America/Danmarkshavn America/Scoresbysund America/Thule
        GM Africa/Banjul
        GN Africa/Conakry
        GP America/Guadeloupe
        GQ Africa/Malabo
        GR Europe/Athens
        GS Atlantic/South_Georgia
        GT America/Guatemala
        GU Pacific/Guam
        GW Africa/Bissau
        GY America/Guyana
        HK Asia/Hong_Kong
        HN America/Tegucigalpa
        HR Europe/Zagreb
        HT America/Port-au-Prince
        HU Europe/Budapest
        ID Asia/Jakarta Asia/Pontianak Asia/Makassar Asia/Jayapura
        IE Europe/Dublin
        IL Asia/Jerusalem
        IM Europe/Isle_of_Man
        IN Asia/Kolkata
        IO Indian/Chagos
        IQ Asia/Baghdad
        IR Asia/Tehran
        IS Atlantic/Reykjavik
        IT Europe/Rome
        JE Europe/Jersey
        JM America/Jamaica
        JO Asia/Amman
        JP Asia/Tokyo
        KE Africa/Nairobi
        KG Asia/Bishkek
        KH Asia/Phnom_Penh
        KI Pacific/Tarawa Pacific/Kanton Pacific/Kiritimati
        KM Indian/Comoro
        KN America/St_Kitts
        KP Asia/Pyongyang
        KR Asia/Seoul
        KW Asia/Kuwait
        KY America/Cayman
        KZ Asia/Almaty Asia/Qyzylorda Asia/Qostanay Asia/Aqtobe Asia/Aqtau Asia/Atyrau Asia/Oral
        LA Asia/Vientiane
        LB Asia/Beirut
        LC America/St_Lucia
        LI Europe/Vaduz
        LK Asia/Colombo
        LR Africa/Monrovia
        LS Africa/Maseru
        LT Europe/Vilnius
        LU Europe/Luxembourg
        LV Europe/Riga
        LY Africa/Tripoli
        MA Africa/Casablanca
        MC Europe/Monaco
        MD Europe/Chisinau
        ME Europe/Podgorica
        MF America/Marigot
        MG Indian/Antananarivo
        MH Pacific/Majuro Pacific/Kwajalein
        MK Europe/Skopje
        ML Africa/Bamako
        MM Asia/Yangon
        MN Asia/Ulaanbaatar Asia/Hovd
        MO Asia/Macau
        MP Pacific/Saipan
        MQ America/Martinique
        MR Africa/Nouakchott
        MS America/Montserrat
        MT Europe/Malta
        MU Indian/Mauritius
        MV Indian/Maldives
        MW Africa/Blantyre
        MX America/Mexico_City America/Cancun America/Merida America/Monterrey America/Matamoros America/Chihuahua America/Ciudad_Juarez America/Ojinaga America/Mazatlan America/Bahia_Banderas America/Hermosillo America/Tijuana
        MY Asia/Kuala_Lumpur Asia/Kuching
        MZ Africa/Maputo
        NA Africa/Windhoek
        NC Pacific/Noumea
        NE Africa/Niamey
        NF Pacific/Norfolk
        NG Africa/Lagos
        NI America/Managua
        NL Europe/Amsterdam
        NO Europe/Oslo
        NP Asia/Kathmandu
        NR Pacific/Nauru
        NU Pacific/Niue
        NZ Pacific/Auckland Pacific/Chatham
        OM Asia/Muscat
        PA America/Panama
        PE America/Lima
        PF Pacific/Tahiti Pacific/Marquesas Pacific/Gambier
        PG Pacific/Port_Moresby Pacific/Bougainville
        PH Asia/Manila
        PK Asia/Karachi
        PL Europe/Warsaw
        PM America/Miquelon
        PN Pacific/Pitcairn
        PR America/Puerto_Rico
        PS Asia/Gaza Asia/Hebron
        PT Europe/Lisbon Atlantic/Madeira Atlantic/Azores
        PW Pacific/Palau
        PY America/Asuncion
        QA Asia/Qatar
        RE Indian/Reunion
        RO Europe/Bucharest
        RS Europe/Belgrade
        RU Europe/Kaliningrad Europe/Moscow Europe/Kirov Europe/Volgograd Europe/Astrakhan Europe/Saratov Europe/Ulyanovsk Europe/Samara Asia/Yekaterinburg Asia/Omsk Asia/Novosibirsk Asia/Barnaul Asia/Tomsk Asia/Novokuznetsk Asia/Krasnoyarsk Asia/Irkutsk Asia/Chita Asia/Yakutsk Asia/Khandyga Asia/Vladivostok Asia/Ust-Nera Asia/Magadan Asia/Sakhalin Asia/Srednekolymsk Asia/Kamchatka Asia/Anadyr
        RW Africa/Kigali
        SA Asia/Riyadh
        SB Pacific/Guadalcanal
        SC Indian/Mahe
        SD Africa/Khartoum
        SE Europe/Stockholm
        SG Asia/Singapore
        SH Atlantic/St_Helena
        SI Europe/Ljubljana
        SJ Arctic/Longyearbyen
        SK Europe/Bratislava
        SL Africa/Freetown
        SM Europe/San_Marino
        SN Africa/Dakar
        SO Africa/Mogadishu
        SR America/Paramaribo
        SS Africa/Juba
        ST Africa/Sao_Tome
        SV America/El_Salvador
        SX America/Lower_Princes
        SY Asia/Damascus
        SZ Africa/Mbabane
        TC America/Grand_Turk
        TD Africa/Ndjamena
        TF Indian/Kerguelen
        TG Africa/Lome
        TH Asia/Bangkok
        TJ Asia/Dushanbe
        TK Pacific/Fakaofo
        TL Asia/Dili
        TM Asia/Ashgabat
        TN Africa/Tunis
        TO Pacific/Tongatapu
        TR Europe/Istanbul
        TT America/Port_of_Spain
        TV Pacific/Funafuti
        TW Asia/Taipei
        TZ Africa/Dar_es_Salaam
        UA Europe/Kyiv Europe/Simferopol
        UG Africa/Kampala
        UM Pacific/Midway Pacific/Wake
        US America/New_York America/Detroit America/Kentucky/Louisville America/Kentucky/Monticello America/Indiana/Indianapolis America/Indiana/Vincennes America/Indiana/Winamac America/Indiana/Marengo America/Indiana/Petersburg America/Indiana/Vevay America/Chicago America/Indiana/Tell_City America/Indiana/Knox America/Menominee America/North_Dakota/Center America/North_Dakota/New_Salem America/North_Dakota/Beulah America/Denver America/Boise America/Phoenix America/Los_Angeles America/Anchorage America/Juneau America/Sitka America/Metlakatla America/Yakutat America/Nome America/Adak Pacific/Honolulu
        UY America/Montevideo
        UZ Asia/Samarkand Asia/Tashkent
        VA Europe/Vatican
        VC America/St_Vincent
        VE America/Caracas
        VG America/Tortola
        VI America/St_Thomas
        VN Asia/Ho_Chi_Minh
        VU Pacific/Efate
        WF Pacific/Wallis
        WS Pacific/Apia
        YE Asia/Aden
        YT Indian/Mayotte
        ZA Africa/Johannesburg
        ZM Africa/Lusaka
        ZW Africa/Harare
        """;

    // tzdata "backward" links that browsers and older OS images still report.
    private const string LegacyAliases = """
        AR America/Buenos_Aires America/Catamarca America/Cordoba America/Jujuy America/Mendoza America/Rosario
        AU Australia/ACT Australia/Canberra Australia/NSW Australia/Queensland Australia/South Australia/Tasmania Australia/Victoria Australia/West
        BD Asia/Dacca
        BR Brazil/East
        BT Asia/Thimbu
        CA America/Montreal America/Nipigon America/Pangnirtung America/Rainy_River America/Thunder_Bay America/Yellowknife Canada/Atlantic Canada/Central Canada/Eastern Canada/Mountain Canada/Newfoundland Canada/Pacific
        CL Chile/Continental
        CN Asia/Chongqing Asia/Chungking Asia/Harbin Asia/Kashgar PRC
        CU Cuba
        EG Egypt
        ER Africa/Asmera
        FM Pacific/Ponape Pacific/Truk
        FO Atlantic/Faeroe
        GB Europe/Belfast GB
        GL America/Godthab
        HK Hongkong
        IE Eire
        IL Asia/Tel_Aviv Israel
        IN Asia/Calcutta
        IR Iran
        IS Iceland
        JM Jamaica
        JP Japan
        KI Pacific/Enderbury
        KR ROK
        LY Libya
        MM Asia/Rangoon
        MN Asia/Ulan_Bator
        MO Asia/Macao
        MX America/Ensenada America/Santa_Isabel Mexico/General
        NP Asia/Katmandu
        NZ NZ
        PL Poland
        PT Portugal
        SG Singapore
        SJ Atlantic/Jan_Mayen
        TM Asia/Ashkhabad
        TR Asia/Istanbul Turkey
        TW ROC
        UA Europe/Kiev Europe/Uzhgorod Europe/Zaporozhye
        UM Pacific/Johnston
        US America/Fort_Wayne America/Indianapolis America/Knox_IN America/Louisville US/Alaska US/Arizona US/Central US/Eastern US/Hawaii US/Mountain US/Pacific
        VN Asia/Saigon
        """;

    private static readonly Dictionary<string, string> Map = Build();

    private static Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in (ZoneTab + "\n" + LegacyAliases).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var zone in parts.Skip(1)) map[zone] = parts[0];
        }
        return map;
    }

    /// <summary>How many zones are known (for tests).</summary>
    public static int Count => Map.Count;

    /// <summary>The ISO-2 country for <paramref name="timeZone"/>, or null for unknown zones and country-less ones
    /// such as "UTC" or "Etc/GMT+5".</summary>
    public static string? CountryOf(string? timeZone)
        => !string.IsNullOrWhiteSpace(timeZone) && Map.TryGetValue(timeZone.Trim(), out var cc) ? cc : null;
}
