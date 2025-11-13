using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RepoXR.Managers;
using Steamworks;
using UnityEngine;

namespace RepoXR;

internal static class Utils
{
    public static byte[] ComputeHash(byte[] input)
    {
        using var sha = SHA256.Create();

        return sha.ComputeHash(input);
    }

    public static string ToHumanReadable(string input)
    {
        var builder = new StringBuilder(input[0].ToString());
        if (builder.Length <= 0)
            return builder.ToString();

        for (var index = 1; index < input.Length; index++)
        {
            var prevChar = input[index - 1];
            var nextChar = index + 1 < input.Length ? input[index + 1] : '\0';

            var isNextLower = char.IsLower(nextChar);
            var isNextUpper = char.IsUpper(nextChar);
            var isPresentUpper = char.IsUpper(input[index]);
            var isPrevLower = char.IsLower(prevChar);
            var isPrevUpper = char.IsUpper(prevChar);

            if (!string.IsNullOrWhiteSpace(prevChar.ToString()) &&
                ((isPrevUpper && isPresentUpper && isNextLower) ||
                 (isPrevLower && isPresentUpper && isNextLower) ||
                 (isPrevLower && isPresentUpper && isNextUpper)))
                builder.Append(' ');

            builder.Append(input[index]);
        }

        return builder.ToString();
    }

    public static string[] ParseConfig(string content)
    {
        var lines = content.Split("\n", StringSplitOptions.RemoveEmptyEntries);

        return (from line in lines
            where !line.TrimStart().StartsWith("#")
            let commentIndex = line.IndexOf('#')
            select commentIndex >= 0 ? line[..commentIndex].Trim() : line.Trim()
            into parsedLine
            where !string.IsNullOrEmpty(parsedLine)
            select parsedLine).ToArray();
    }

    public static string GetControlSpriteString(string controlPath)
    {
        if (string.IsNullOrEmpty(controlPath))
            return "<b><u>NOT BOUND</u></b>";

        var path = Regex.Replace(controlPath.ToLowerInvariant(), @"<[^>]+>([^ ]+)", "$1");
        var hand = path.Split('/')[0].TrimStart('{').TrimEnd('}');
        controlPath = Regex.Replace(string.Join("/", path.Split('/').Skip(1)), @"{(.*)}", "$1");

        var id = (hand, controlPath) switch
        {
            ("lefthand", "primary2daxis" or "thumbstick") => "leftStick",
            ("lefthand", "primary2daxisclick" or "thumbstickclicked") => "leftStickClick",
            ("lefthand", "primary2daxis/up" or "thumbstick/up") => "leftStickUp",
            ("lefthand", "primary2daxis/down" or "thumbstick/down") => "leftStickDown",
            ("lefthand", "primary2daxis/left" or "thumbstick/left") => "leftStickLeft",
            ("lefthand", "primary2daxis/right" or "thumbstick/right") => "leftStickRight",
            ("lefthand", "primarybutton" or "primarypressed") => "leftPrimaryButton",
            ("lefthand", "secondarybutton" or "secondarypressed") => "leftSecondaryButton",
            ("lefthand", "triggerbutton" or "trigger" or "triggerpressed") => "leftTrigger",
            ("lefthand", "gripbutton" or "grip" or "grippressed") => "leftGrip",

            ("righthand", "primary2daxis" or "thumbstick") => "rightStick",
            ("righthand", "primary2daxisclick" or "thumbstickclicked") => "rightStickClick",
            ("righthand", "primary2daxis/up" or "thumbstick/up") => "rightStickUp",
            ("righthand", "primary2daxis/down" or "thumbstick/down") => "rightStickDown",
            ("righthand", "primary2daxis/left" or "thumbstick/left") => "rightStickLeft",
            ("righthand", "primary2daxis/right" or "thumbstick/right") => "rightStickRight",
            ("righthand", "primarybutton" or "primarypressed") => "rightPrimaryButton",
            ("righthand", "secondarybutton" or "secondarypressed") => "rightSecondaryButton",
            ("righthand", "triggerbutton" or "trigger" or "triggerpressed") => "rightTrigger",
            ("righthand", "gripbutton" or "grip" or "grippressed") => "rightGrip",

            (_, "menu" or "menubutton" or "menupressed") => "menuButton",

            _ => "unknown"
        };

        return $"""<sprite name="{id}">""";
    }

    public static bool GetControlHand(string controlPath, out HapticManager.Hand hand)
    {
        hand = HapticManager.Hand.Both;

        if (string.IsNullOrEmpty(controlPath))
            return false;

        var path = Regex.Replace(controlPath.ToLowerInvariant(), @"<[^>]+>([^ ]+)", "$1");
        var handText = path.Split('/')[0].TrimStart('{').TrimEnd('}');

        hand = handText == "lefthand" ? HapticManager.Hand.Left : HapticManager.Hand.Right;

        return true;
    }

    public static T? ExecuteWithSteamAPI<T>(Func<T> func)
    {
        try
        {
            var isValid = SteamClient.IsValid;

            if (!isValid)
                SteamClient.Init(3241660, false);

            var result = func();

            if (!isValid)
                SteamClient.Shutdown();

            return result;
        }
        catch
        {
            return default;
        }
    }

    public static bool Collide(Collider lhs, params Collider[] rhs)
    {
        return rhs.Any(collider => Physics.ComputePenetration(lhs, lhs.transform.position, lhs.transform.rotation,
            collider,
            collider.transform.position, collider.transform.rotation, out _, out _));
    }

    public static void DisableScanlines(this SemiUI ui)
    {
        if (ui.GetComponentInChildren<UIScanlines>() is not { } scanlines)
            return;

        scanlines.enabled = false;
        scanlines.image.color = Color.clear;
    }

    public static void SetUIAnchoredPosition(this SemiUI ui, Vector2 anchoredPosition)
    {
        var hidePosition = ui.hidePosition - ui.showPosition;
        var rect = ui.GetComponent<RectTransform>();

        rect.anchoredPosition = anchoredPosition;
        ui.showPosition = Vector2.zero;
        ui.hidePosition = hidePosition;
        ui.Start();
    }

    public static void ReplaceOrInsert<T>(this List<T> list, T item, Predicate<T> match)
    {
        var index = list.FindIndex(match);
        if (index >= 0)
            list[index] = item;
        else
            list.Add(item);
    }

    public static Color GetTextColor(Color baseColor, float minBrightness = 0.6f, float alpha = 1f)
    {
        var brightness = 0.2126f * baseColor.r + 0.7152f * baseColor.g + 0.0722f * baseColor.b;

        if (brightness < minBrightness)
        {
            var blendAmount = Mathf.Clamp01((minBrightness - brightness) / minBrightness);
            baseColor = Color.Lerp(baseColor, Color.white, blendAmount);
        }

        baseColor.a = alpha;
        return baseColor;
    }
}