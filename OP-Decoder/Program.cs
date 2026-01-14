// See https://aka.ms/new-console-template for more information
using OpenProtocolInterpreter;
using OpenProtocolInterpreter.Communication;
using OpenProtocolInterpreter.MultiSpindle;

Console.WriteLine("Hello, World!");
var interpreter = new MidInterpreter().UseAllMessages();
var midPackage = @"02100101001000000000010202L000000000038500000000000030304003050000060000070080130000903000010000000110008012001001300090142025-10-20:11:41:29152025-12-05:15:30:04160012117118010111026000100090020111026400100090";
var myMid04 = interpreter.Parse<Mid0101>(midPackage);
//MID 0004 is an error mid which contains which MID Failed and its error code
//Int value of the Failed Mid
var test = false;
//An enum with Error Code
