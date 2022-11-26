using DTC.Models.DCS;
using DTC.Models.FA18.Waypoints;
using System;
using System.Drawing;
using System.Text;

namespace DTC.Models.FA18.Upload
{
	public class WaypointBuilder : BaseBuilder
	{
		private FA18Configuration _cfg;

		public WaypointBuilder(FA18Configuration cfg, IAircraftDeviceManager aircraft, StringBuilder sb) : base(aircraft, sb)
		{
			_cfg = cfg;
		}

		private void selectWp0(Device rmfd, int i)
        {
			if (i < 140) // It might not notice on the first pass, so we go around once more
			{
				AppendCommand(StartCondition("NOT_AT_WP0"));
				AppendCommand(rmfd.GetCommand("OSB-13"));
				AppendCommand(EndCondition("NOT_AT_WP0"));
				selectWp0(rmfd, i + 1);
			}
		}
		public override void Build()
		{
			var wpts = _cfg.Waypoints.Waypoints;
			var wptStart = _cfg.Waypoints.SteerpointStart;
			var wptEnd = wptStart + wpts.Count;

			if (wpts.Count == 0)
			{
				return;
			}

			var wptDiff = wptEnd - wptStart;
			
			var ufc = _aircraft.GetDevice("UFC");
			var rmfd = _aircraft.GetDevice("RMFD");
			AppendCommand(rmfd.GetCommand("OSB-18")); // MENU
			AppendCommand(rmfd.GetCommand("OSB-18")); // MENU
			AppendCommand(rmfd.GetCommand("OSB-02")); // HSI

			AppendCommand(rmfd.GetCommand("OSB-10")); // DATA
			AppendCommand(rmfd.GetCommand("OSB-07")); // WYPT
			AppendCommand(rmfd.GetCommand("OSB-05")); // UFC

			selectWp0(rmfd, 0);
			for (var i = 0; i< wptStart; i++)
            {
				AppendCommand(rmfd.GetCommand("OSB-12"));
			}

			for (var i = 0; i < wptDiff; i++)
			{
				Waypoint wpt;
                wpt = wpts[i];

				if (wpt.Blank)
				{
					continue;
				}
				
				AppendCommand(ufc.GetCommand("Opt1"));
				AppendCommand(Wait());
				AppendCommand(BuildCoordinate2(ufc, wpt.Latitude)); // FG - precise coordinates management with BuildCoordinate2
				AppendCommand(ufc.GetCommand("ENT"));
				AppendCommand(WaitLong());

				AppendCommand(BuildCoordinate2(ufc, wpt.Longitude)); // FG - precise coordinates management with BuildCoordinate2
				AppendCommand(ufc.GetCommand("ENT"));
				AppendCommand(WaitLong());

				AppendCommand(ufc.GetCommand("Opt3"));
				AppendCommand(ufc.GetCommand("Opt1"));
				AppendCommand(BuildDigits(ufc, wpt.Elevation.ToString()));
				AppendCommand(ufc.GetCommand("ENT"));
				AppendCommand(Wait());

				AppendCommand(rmfd.GetCommand("OSB-12")); // Next Waypoint
			}
			for (var i = 0; i < wptDiff; i++)
			{
				AppendCommand(rmfd.GetCommand("OSB-13")); // Prev Waypoint
			}

            AppendCommand(Wait());
			AppendCommand(rmfd.GetCommand("OSB-18"));
            AppendCommand(Wait());
			AppendCommand(rmfd.GetCommand("OSB-18"));
			AppendCommand(rmfd.GetCommand("OSB-15"));
		}

		private string BuildCoordinate(Device ufc, string coord)
		{
			var sb = new StringBuilder();

			var latStr = RemoveSeparators(coord.Replace(" ", ""));
			var i = 0;
			var lon = false;
			var longLon = false;

			foreach (var c in latStr.ToCharArray())
			{
				if (c == 'N')
				{
					sb.Append(ufc.GetCommand("2"));
					i = 0;
				}
				else if (c == 'S')
				{
					sb.Append(ufc.GetCommand("8"));
					i = 0;
				}
				else if (c == 'E')
				{
					sb.Append(ufc.GetCommand("6"));
					i = 0;
					lon = true;	
				}
				else if (c == 'W')
				{
					sb.Append(ufc.GetCommand("4"));
					i = 0;
					lon = true;
				}
				else
				{
					if (i <= 5 || (i<= 6 && longLon)) { 
						if (!(i == 0 && c == '0' && lon))
                        {
							if(i == 0 && c == '1' && lon) longLon = true;

							sb.Append(ufc.GetCommand(c.ToString()));
							i++;
							lon = false;
						}					
							
					}
				}
			}

			return sb.ToString();
		}

		private string BuildCoordinate2(Device ufc, string coord)
		{
			// FG - rework to add the option of precise coordinate input
			/*
			 * Input of coordinates varies in precise mode if the aircraft is in DCML on SEC
			 * This here will only work in DCML, as it is the default mode in DCS, and there no efficient way to check which mode the aircraft is in
			 * 
			 * DCML
			 *	Not PRECISE : N123456 > ENT
			 *	PRECISE : N1234 > ENT > 5678
			 *	
			 * SEC
			 *	Not PRECISE : N123456 > ENT
			 *	PRECISE : N123456 > ENT > 78
			*/
			//

			coord = $"{coord}88";

			StringBuilder sb = new StringBuilder();

			string sInputCleaned = RemoveSeparators(coord.Replace(" ", ""));
			if (sInputCleaned.Length < 1) // we only require the hemisphere or meridian side, for the rest we will remplace any missing digit by 0
				throw new Exception($"Input coordinate string is incorrect {coord}");

			// hemisphere or meridian side
			int iLongitudeOffset = 0; // one more digit for longitudes
			char c = sInputCleaned[0];
			if (c == 'N')
			{
				sb.Append(ufc.GetCommand("2"));
			}
			else if (c == 'S')
			{
				sb.Append(ufc.GetCommand("8"));
			}
			else if (c == 'E')
			{
				sb.Append(ufc.GetCommand("6"));
				iLongitudeOffset = 1;
			}
			else if (c == 'W')
			{
				sb.Append(ufc.GetCommand("4"));
				iLongitudeOffset = 1;
			}
			else
			{
				throw new Exception($"Input coordinate string is incorrect {coord}");
			}

			int i = 1;
			// first 4 or 5 digits
			while (i <= 4 + iLongitudeOffset)
			{
				BuildCoordinate2_AddDigit(ufc, sInputCleaned, i, sb);
				i++;
			}

			// not precise, add 2 more digits
			sb.Append(StartCondition("WPT_NOT_PRECISE"));
			while (i <= 6 + iLongitudeOffset)
			{
				BuildCoordinate2_AddDigit(ufc, sInputCleaned, i, sb);
				i++;
			}
			sb.Append(EndCondition("WPT_NOT_PRECISE"));

			// precise, enter and add 4 more digits
			sb.Append(StartCondition("WPT_PRECISE"));
			sb.Append(ufc.GetCommand("ENT"));
			while (i <= 8 + iLongitudeOffset)
			{
				BuildCoordinate2_AddDigit(ufc, sInputCleaned, i, sb);
				i++;
			}
			sb.Append(EndCondition("WPT_PRECISE"));
			
			return sb.ToString();
		}

		private void BuildCoordinate2_AddDigit(Device ufc, string sInputCleaned, int iInputPosition, StringBuilder sb)
		{
			char c = '0';
			if (!string.IsNullOrEmpty(sInputCleaned) && iInputPosition < sInputCleaned.Length)
			{
				c = sInputCleaned[iInputPosition];
			}

			if (c < 48 || c > 57)
				throw new Exception($"Input coordinate string is incorrect {sInputCleaned}");

			sb.Append(ufc.GetCommand(c.ToString()));
		}

	}
}
