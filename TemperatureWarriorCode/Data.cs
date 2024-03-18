using System;
using Meadow.Foundation;

namespace TemperatureWarriorCode {
    class Data {

        //WEB VARIABLES
        public static string IP = null;
        public static int Port = 2550;

        //ROUND VARIABLES
        public static string[] temp_max = { "18" }; // In ºC
        public static String[] temp_min = { "16" }; // In ºC
        public static int display_refresh = 100; // In ms
        public static int refresh = 100; // In ms
        public static String[] round_time = { "60" }; // in s

        //START ROUND VARIABLES
        public static bool is_working = false;
        public static string temp_act = "0"; // In ºC
        public static int time_left; // in s
        public static int time_in_range_temp = 0; //In ms.
                                           
        //COLORS FOR DISPLAY
        public static Color[] colors = new Color[4]
        {
            Color.FromHex("#67E667"),
            Color.FromHex("#00CC00"),
            Color.FromHex("#269926"),
            Color.FromHex("#008500")
        };
    }
}
