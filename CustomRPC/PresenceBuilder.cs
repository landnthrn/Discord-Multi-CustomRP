using DiscordRPC;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DButton = DiscordRPC.Button;

namespace CustomRPC
{
    /// <summary>
    /// Builds a <see cref="RichPresence"/> from a slot using CustomRP validation rules.
    /// </summary>
    public static class PresenceBuilder
    {
        const string U00A0 = "\u00A0";
        const string U200B = "\u200B";

        public class BuildResult
        {
            public bool Success { get; set; }
            public RichPresence Presence { get; set; }
            public List<DButton> Buttons { get; set; } = new List<DButton>();
            public string ErrorMessage { get; set; } = "";
            public string ImageErrorField { get; set; } = "";
            public bool NeedsLocalTimeTimer { get; set; }
        }

        public static BuildResult Build(
            PresenceSlot slot,
            DateTime timestampConnected,
            DateTime timestampStarted,
            DateTime customTimestampStartLocal,
            DateTime customTimestampEndLocal,
            bool customTimestampEndEnabled,
            int detailsUrlMaxLength,
            int stateUrlMaxLength,
            int largeUrlMaxLength,
            int smallUrlMaxLength,
            int button1UrlMaxLength,
            int button2UrlMaxLength)
        {
            var result = new BuildResult();

            string details = NormalizeLeadingSpace(slot.Details);
            string state = NormalizeLeadingSpace(slot.State);

            if (slot.PartySize > slot.PartyMax)
                slot.PartyMax = slot.PartySize;

            slot.DetailsUrl = (slot.DetailsUrl ?? "").Trim();
            slot.StateUrl = (slot.StateUrl ?? "").Trim();
            slot.LargeImageKey = (slot.LargeImageKey ?? "").Trim();
            slot.LargeImageUrl = (slot.LargeImageUrl ?? "").Trim();
            slot.SmallImageKey = (slot.SmallImageKey ?? "").Trim();
            slot.SmallImageUrl = (slot.SmallImageUrl ?? "").Trim();
            slot.Button1Url = (slot.Button1Url ?? "").Trim();
            slot.Button2Url = (slot.Button2Url ?? "").Trim();

            var rp = new RichPresence
            {
                Name = slot.Name,
                Type = slot.ActivityType,
                StatusDisplay = slot.StatusDisplay,
                Details = details,
                State = state,
                Party = new Party
                {
                    ID = (slot.PartySize > 0 && slot.PartyMax > 0) ? "CustomRP" : "",
                    Size = slot.PartySize,
                    Max = slot.PartyMax,
                },
            };

            slot.DetailsUrl = ProcessUrl(slot.DetailsUrl, detailsUrlMaxLength);
            slot.StateUrl = ProcessUrl(slot.StateUrl, stateUrlMaxLength);

            try
            {
                rp.DetailsUrl = slot.DetailsUrl;
                rp.StateUrl = slot.StateUrl;
            }
            catch (Exception e)
            {
                result.ErrorMessage = e.Message;
                return result;
            }

            try
            {
                slot.SmallImageKey = NormalizeImageKey(slot.SmallImageKey, "Small", out string smallField);
                if (smallField != null)
                {
                    result.ImageErrorField = smallField;
                    throw new ArgumentException(smallField);
                }

                slot.LargeImageKey = NormalizeImageKey(slot.LargeImageKey, "Large", out string largeField);
                if (largeField != null)
                {
                    result.ImageErrorField = largeField;
                    throw new ArgumentException(largeField);
                }

                slot.LargeImageUrl = ProcessUrl(slot.LargeImageUrl, largeUrlMaxLength);
                slot.SmallImageUrl = ProcessUrl(slot.SmallImageUrl, smallUrlMaxLength);

                rp.Assets = new Assets
                {
                    LargeImageKey = Proxify(slot.LargeImageKey),
                    LargeImageText = slot.LargeImageText,
                    LargeImageUrl = slot.LargeImageUrl,
                    SmallImageKey = Proxify(slot.SmallImageKey),
                    SmallImageText = slot.SmallImageText,
                    SmallImageUrl = slot.SmallImageUrl,
                };
            }
            catch (Exception e)
            {
                if (e is ArgumentException)
                    result.ImageErrorField = e.Message;
                else
                    result.ErrorMessage = e.Message;
                return result;
            }

            slot.Button1Url = ProcessUrl(slot.Button1Url, button1UrlMaxLength);
            slot.Button2Url = ProcessUrl(slot.Button2Url, button2UrlMaxLength);

            try
            {
                if (!string.IsNullOrEmpty(slot.Button1Text) && !string.IsNullOrEmpty(slot.Button1Url))
                    result.Buttons.Add(new DButton { Label = slot.Button1Text, Url = slot.Button1Url });

                if (!string.IsNullOrEmpty(slot.Button2Text) && !string.IsNullOrEmpty(slot.Button2Url))
                    result.Buttons.Add(new DButton { Label = slot.Button2Text, Url = slot.Button2Url });
            }
            catch
            {
                result.ErrorMessage = Strings.errorInvalidURL;
                return result;
            }

            rp.Buttons = result.Buttons.ToArray();

            switch (slot.TimestampType)
            {
                case TimestampType.SinceLastConnection:
                    rp.Timestamps = new Timestamps(timestampConnected);
                    break;
                case TimestampType.SinceStartup:
                    rp.Timestamps = new Timestamps(timestampStarted);
                    break;
                case TimestampType.SincePresenceUpdate:
                    rp.Timestamps = Timestamps.Now;
                    break;
                case TimestampType.LocalTime:
                    rp.Timestamps = new Timestamps(DateTime.UtcNow.Subtract(new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second)));
                    result.NeedsLocalTimeTimer = true;
                    break;
                case TimestampType.Custom:
                    DateTime customTimestampStart = customTimestampStartLocal.ToUniversalTime();
                    DateTime customTimestampEnd = customTimestampEndLocal.ToUniversalTime();
                    if (customTimestampEndEnabled)
                        rp.Timestamps = new Timestamps(customTimestampStart, customTimestampEnd);
                    else
                        rp.Timestamps = customTimestampStart.CompareTo(DateTime.UtcNow) < 0
                            ? new Timestamps(customTimestampStart)
                            : new Timestamps(DateTime.UtcNow, customTimestampStart);
                    break;
            }

            result.Success = true;
            result.Presence = rp;
            return result;
        }

        static string NormalizeLeadingSpace(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            if (text.StartsWith(U00A0) && text.Length < 128)
                return U200B + text;
            if (text.StartsWith(U00A0 + U00A0))
                return U200B + text.Substring(1);

            return text;
        }

        static string ProcessUrl(string url, int maxLength)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            if (!url.Contains("://"))
                url = "https://" + url;

            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri tempUri))
                    url = tempUri.AbsoluteUri.Replace(tempUri.Host, tempUri.IdnHost);
            }
            catch
            {
            }

            return url.Substring(0, Math.Min(maxLength, url.Length));
        }

        static string Proxify(string key)
        {
            if (key != null)
                return Regex.Replace(key, @"//((cdn)|(media))\.discordapp\.((com)|(net))/", "//customrp.xyz/proxy/");

            return key;
        }

        static string NormalizeImageKey(string key, string fieldName, out string errorField)
        {
            errorField = null;
            if (string.IsNullOrEmpty(key))
                return key;

            if (!Uri.TryCreate(key, UriKind.Absolute, out Uri tempUri))
                return key;

            if (IsMpExternalStringOverLimit(tempUri))
            {
                errorField = fieldName;
                return key;
            }

            return tempUri.AbsoluteUri.Replace(tempUri.Host, tempUri.IdnHost);
        }

        static bool IsMpExternalStringOverLimit(Uri uri)
        {
            return $"mp:external/43 characters that probably represent an id/{Uri.EscapeDataString(uri.Query)}/{uri.Scheme}/{(uri.IdnHost == "media.discordapp.net" ? "cdn.discordapp.com" : uri.IdnHost)}{uri.AbsolutePath}".Length > 256;
        }
    }
}
