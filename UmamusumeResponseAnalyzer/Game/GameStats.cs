﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console;

namespace UmamusumeResponseAnalyzer.Game
{

    public class TrainStats
    {
        public int[] FiveValueGain;
        public int PtGain;
        public int[] FiveValueGainNonScenario;
        public int PtGainNonScenario;
        public int VitalGain;
        public int FailureRate;
        public double ScoreAssumeSuccessNoVital()
        {
            return FiveValueGain.Sum() + PtGain * 0.7;
        }
        public double ScoreNoVital()
        {
            const double scoreAssumeFailed = -100;//掉1心情5属性，还相当于浪费一回合，有各种隐性影响
            return ScoreAssumeSuccessNoVital() * (1 - 0.01 * FailureRate) + 0.01 * FailureRate * scoreAssumeFailed;
        }
        public double Score(int currentVital, int maxVital)
        {
            double scoreAssumeFailed = -100;
            double scoreAssumeSuccess = FiveValueGain.Sum() + PtGain * 0.7;
            return (scoreAssumeSuccess - ScoreUtils.ScoreOfVital(currentVital, maxVital) + ScoreUtils.ScoreOfVital(currentVital + VitalGain, maxVital)) * (1 - 0.01 * FailureRate) + 0.01 * FailureRate * scoreAssumeFailed;
        }
    }
    public class TurnStats
    {
        public bool isTraining;//是否为训练回合（否则为比赛回合）
        public int motivation;//干劲（心情）
        public TrainStats[] fiveTrainStats;//五个训练分别加多少

        public int playerChoice;//玩家最终点了哪个训练
        public bool isTrainingFailed;//是否训练失败


        //凯旋门
        public bool[] larc_zuoyueAtTrain;//佐岳是否在这个训练
        public bool larc_playerChoiceSS;//这个回合玩家是不是点的ss训练
        public bool larc_isFullSS;//这个回合ss训练有没有5个头
        public bool larc_isSSS;//这个回合是不是sss训练
        public int larc_zuoyueEvent;//是否召唤出佐岳充电事件。有好几种：0没事件，1充电，2充电加心情，3一百适性pt，4海外，5第一次启动



        //大师杯
        //三女神等级
        public int venus_yellowVenusLevel;
        public int venus_redVenusLevel;
        public int venus_blueVenusLevel;

        public bool venus_isEffect102;//是否为女神情热状态
        public int venus_venusTrain;//女神在哪个训练
        public bool venus_isVenusCountConcerned;//非情热，且无蓝女神
        public bool venus_venusEvent;//成功召唤出女神三选一事件
        public TurnStats() {
            isTraining = false;
            motivation = 0;
            playerChoice = -1;
            isTrainingFailed = false;
            fiveTrainStats = new TrainStats[5];

            larc_zuoyueAtTrain = new bool[5];//佐岳是否在这个训练
            for(int j = 0; j < 5; j++) larc_zuoyueAtTrain[j] = false;
            larc_playerChoiceSS = false;
            larc_isFullSS = false;
            larc_isSSS = false;
            larc_zuoyueEvent = 0;

            venus_isEffect102 = false;
            venus_yellowVenusLevel = 0;
            venus_redVenusLevel = 0;
            venus_blueVenusLevel = 0;
            venus_venusTrain = -100;
            venus_isVenusCountConcerned = true;
            venus_venusEvent = false;
        }
    }
    public class GameStats
    {
        public static int whichScenario = 0;
        public static int currentTurn = 0;
        public static TurnStats[] stats=new TurnStats[79];//从1开始

        public static void print()
        {
            //统计掉心情次数
            {
                int motDropCount = 0;
                for (int i = currentTurn; i >= 2; i--)
                {
                    if (stats[i] == null)
                    {
                        break;
                    }
                    if (stats[i - 1] == null)
                    {
                        break;
                    }
                    if (stats[i].motivation < stats[i - 1].motivation)
                        motDropCount += (stats[i - 1].motivation - stats[i].motivation);
                }
                AnsiConsole.MarkupLine($"这局掉了[yellow]{motDropCount}[/]次心情（忽略刚掉就回的情况）");
            }

            //统计体力消耗和赌训练的次数
            {
                int totalVitalGain = 0;
                int totalGambleTimes = 0;
                int totalFailureRate = 0;
                int totalFailureTimes = 0;
                for (int i = currentTurn - 1; i >= 1; i--)
                {
                    if (stats[i] == null)
                    {
                        break;
                    }
                    if (!GameGlobal.TrainIds.Any(x => x == stats[i].playerChoice)) //没训练
                        continue;
                    var trainStat = stats[i].fiveTrainStats[GameGlobal.ToTrainIndex[stats[i].playerChoice]];
                    if (!stats[i].isTrainingFailed)
                        totalVitalGain += trainStat.VitalGain;
                    else
                        totalFailureTimes += 1;
                    int failRate = trainStat.FailureRate;
                    if(failRate>0)
                    {
                        totalGambleTimes += 1;
                        totalFailureRate += failRate;
                    }
                }
                AnsiConsole.MarkupLine($"这局赌了[yellow]{totalGambleTimes}[/]次训练，失败了[yellow]{totalFailureTimes}[/]次，总失败率为[yellow]{totalFailureRate}[/]%");
                AnsiConsole.MarkupLine($"训练消耗总体力：[yellow]{-totalVitalGain}[/]");
            }
            if (whichScenario == (int)ScenarioType.GrandMasters)
            {
                //统计召唤女神次数与来的次数
                {
                    int concernedTurnTotal = 0, venusEventCount = 0;
                    for (int turn = 1; turn < currentTurn; turn++)//不考虑当前回合，因为当前回合还未选训练
                    {
                        var turnStat = stats[turn];
                        if (turnStat == null) continue;//有可能这局中途才打开小黑板
                        if (turnStat.isTraining && turnStat.venus_isVenusCountConcerned && turnStat.venus_venusTrain == turnStat.playerChoice)
                        {

                            concernedTurnTotal += 1;
                            if (turnStat.venus_venusEvent)
                            {
                                venusEventCount += 1;
                            }
                        }
                    }
                    AnsiConsole.MarkupLine($"一共召唤了[aqua]{concernedTurnTotal}[/]次女神，女神来了[aqua]{venusEventCount}[/]次");
                }
                //统计女神持续回合数
                {
                    int[] contTurns = new int[100];
                    int maxContTurns = 0;
                    for (int i = 0; i < 100; i++) { contTurns[i] = 0; }

                    bool isUnfinishedLast = true;//最后一次不完整，不统计
                    int contTurn = 0;
                    for (int i = currentTurn; i >= 1; i--)
                    {
                        //第一个回合不可能是女神情热，所以这个判断条件没有问题
                        if (stats[i] == null)
                        {
                            break;
                        }
                        if (!stats[i].venus_isEffect102)
                        {
                            if (!isUnfinishedLast)
                            {
                                contTurns[contTurn]++;
                                if (contTurn > maxContTurns) maxContTurns = contTurn;
                            }
                            contTurn = 0;
                            isUnfinishedLast = false;
                        }
                        else contTurn++;
                    }
                    string linetoPrint = $"女神持续回合数统计：";
                    for (int i = 1; i <= maxContTurns; i++)
                    {
                        linetoPrint += $"[green]{i}[/]回合[yellow]{contTurns[i]}[/]次，";
                    }
                    AnsiConsole.MarkupLine(linetoPrint);
                }

                //统计训练属性pt收益
                {
                    int[] fiveGain = new int[5] { 0, 0, 0, 0, 0 }; //总训练
                    int ptGain = 0;
                    int[] fiveGainSpirit = new int[5] { 0, 0, 0, 0, 0 }; //碎片
                    int ptGainSpirit = 0;
                    for (int turn = currentTurn - 1; turn >= 1; turn--)
                    {
                        if (stats[turn] == null)
                        {
                            break;
                        }
                        if (stats[turn].fiveTrainStats == null || stats[turn].fiveTrainStats[0] == null || stats[turn].fiveTrainStats[0].FiveValueGain == null)
                        {
                            continue;
                        }
                        if (!GameGlobal.TrainIds.Any(x => x == stats[turn].playerChoice)) //没训练
                            continue;
                        if (stats[turn].isTrainingFailed)//训练失败
                            continue;
                        var trainStat = stats[turn].fiveTrainStats[GameGlobal.ToTrainIndex[stats[turn].playerChoice]];
                        double[] venusLevelBonus = new double[6] { 0, 0.05, 0.08, 0.11, 0.13, 0.15 };//女神等级训练加成
                        double venusBonus = venusLevelBonus[stats[turn].venus_blueVenusLevel] + venusLevelBonus[stats[turn].venus_yellowVenusLevel] + venusLevelBonus[stats[turn].venus_redVenusLevel];

                        for (int i = 0; i < 5; i++)
                        {
                            int gain = trainStat.FiveValueGain[i];
                            int gainNonScenario = trainStat.FiveValueGainNonScenario[i];
                            int gainNonSpirit = (int)(((double)gainNonScenario) * (1 + venusBonus));
                            int gainSpirit = gain - gainNonSpirit;

                            fiveGain[i] += gain;
                            fiveGainSpirit[i] += gainSpirit;
                        }
                        {
                            int gain = trainStat.PtGain;
                            int gainNonScenario = trainStat.PtGainNonScenario;
                            int gainNonSpirit = (int)(((double)gainNonScenario) * (1 + venusBonus));
                            int gainSpirit = gain - gainNonSpirit;

                            ptGain += gain;
                            ptGainSpirit += gainSpirit;
                        }

                    }

                    var table = new Table();
                    int tableWidth = 7;
                    table.AddColumns(
                          new TableColumn($" ").Width(12),
                          new TableColumn($"速").Width(tableWidth),
                          new TableColumn($"耐").Width(tableWidth),
                          new TableColumn($"力").Width(tableWidth),
                          new TableColumn($"根").Width(tableWidth),
                          new TableColumn($"智").Width(tableWidth),
                          new TableColumn($"总").Width(tableWidth),
                          new TableColumn($"pt").Width(tableWidth)
                          );
                    {
                        var outputItems = new string[8];
                        outputItems[0] = "总训练";
                        for (int j = 0; j < 5; j++)
                            outputItems[j + 1] = $"{fiveGain[j]}";
                        outputItems[6] = $"{fiveGain.Sum()}";
                        outputItems[7] = $"{ptGain}";
                        table.AddRow(outputItems);
                    }
                    {
                        var outputItems = new string[8];
                        outputItems[0] = "碎片加成";
                        for (int j = 0; j < 5; j++)
                            outputItems[j + 1] = $"{fiveGainSpirit[j]}";
                        outputItems[6] = $"{fiveGainSpirit.Sum()}";
                        outputItems[7] = $"{ptGainSpirit}";
                        table.AddRow(outputItems);
                    }

                    AnsiConsole.Write(table);
                }
            }
            if (whichScenario == (int)ScenarioType.LArc)
            {
                //几次ss训练，几次sss，连着几回合没sss
                int fullSSCount = 0;
                int SSSCount = 0;
                int contNonSSS = 0;
                for (int i = currentTurn; i >= 1; i--)
                {
                    if (stats[i] == null)
                    {
                        break;
                    }

                    if (stats[i].larc_isFullSS && stats[i].larc_playerChoiceSS)//一次完整的ss训练
                    {
                        fullSSCount += 1;
                        if (stats[i].larc_isSSS) SSSCount += 1;
                        if (SSSCount == 0) contNonSSS += 1;
                    }
                }
                AnsiConsole.MarkupLine($"一共进行了[aqua]{fullSSCount}[/]次SS训练，其中[aqua]{SSSCount}[/]次为SSS，已经连续[#80ff00]{contNonSSS}[/]次不是SSS");


                //佐岳点了几次，来了几次
                int zuoyueClickedTimesNonAbroad = 0;//非海外点了几次，不算启动
                int zuoyueClickedTimesAbroad = 0;//海外点了几次
                int zuoyueChargedTimes = 0;//友人冲了几次电（非海外）
                int zuoyueEventTimesAbroad = 0;//海外来了几次
                int zuoyueShixingPtNonAbroad = 0;//非海外加了几次适性pt

                for (int turn = currentTurn; turn >= 1; turn--)
                {
                    if (stats[turn] == null)
                    {
                        break;
                    }
                    bool isAbroad = (turn >= 37 && turn <= 43) || (turn >= 61 && turn <= 67);

                    if (!GameGlobal.TrainIds.Any(x => x == stats[turn].playerChoice)) //没训练
                        continue;
                    if (stats[turn].isTrainingFailed)//训练失败
                        continue;
                    if (!stats[turn].larc_zuoyueAtTrain[GameGlobal.ToTrainIndex[stats[turn].playerChoice]])
                        continue;//没点佐岳
                    if (stats[turn].larc_zuoyueEvent == 5)//启动事件
                        continue;//没点佐岳

                    if(isAbroad)
                    {
                        zuoyueClickedTimesAbroad += 1;
                        if (stats[turn].larc_zuoyueEvent == 4)
                            zuoyueEventTimesAbroad += 1;
                    }
                    else
                    {
                        zuoyueClickedTimesNonAbroad += 1;
                        if (stats[turn].larc_zuoyueEvent == 1 || stats[turn].larc_zuoyueEvent == 2)
                            zuoyueChargedTimes += 1;
                        else if (stats[turn].larc_zuoyueEvent == 3)
                            zuoyueShixingPtNonAbroad += 1;
                    }
                }

                AnsiConsole.MarkupLine($"远征点了[#80ff00]{zuoyueClickedTimesAbroad}[/]次佐岳，加了[#80ff00]{zuoyueEventTimesAbroad}[/]次适性pt");
                AnsiConsole.MarkupLine($"非远征点了[aqua]{zuoyueClickedTimesNonAbroad}[/]次佐岳，充了[aqua]{zuoyueChargedTimes}[/]次电，加了[aqua]{zuoyueShixingPtNonAbroad}[/]次适性pt");
            }
        }
    }

}