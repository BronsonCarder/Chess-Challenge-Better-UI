using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example;
public class Stormwind : IChessBot
{
    struct TTEntry
    {
        public ulong key;
        public Move move;
        public int depth, score, bound;
        public TTEntry(ulong _key, Move _move, int _depth, int _score, int _bound)
        {
            key = _key; move = _move; depth = _depth; score = _score; bound = _bound;
        }
    }

    const int entries = (1 << 20);
    readonly TTEntry[] tt = new TTEntry[entries];

    public Move bestMoveRoot;

    public Move Think(Board board, Timer timer)
    {
        for (int depth = 1; depth <= 42; depth++)
        {
            Negamax(board, timer, depth, 0, -99999, 99999);

            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30)
                break;
        }

        return bestMoveRoot;
    }

    static int GetMaterial(Board board, bool isWhite)
    {
        //Max value 4100
        return board.GetPieceList(PieceType.Pawn, isWhite).Count * 100
            + board.GetPieceList(PieceType.Knight, isWhite).Count * 300
            + board.GetPieceList(PieceType.Bishop, isWhite).Count * 400
            + board.GetPieceList(PieceType.Rook, isWhite).Count * 500
            + board.GetPieceList(PieceType.Queen, isWhite).Count * 900;
    }

    static int EvaluatePosition(Board board)
    {
        bool isWhite = board.IsWhiteToMove;
        int myMaterial = GetMaterial(board, isWhite),
        opMaterial = GetMaterial(board, !isWhite);
        Square myKing = board.GetKingSquare(isWhite),
        opKing = board.GetKingSquare(!isWhite);
        PieceList pawns = board.GetPieceList(PieceType.Pawn, isWhite);

        int currentValue = myMaterial - opMaterial;

        int opKingCenterDistFile = Math.Max(3 - opKing.File, opKing.File - 4);
        int opKingCenterDistRank = Math.Max(3 - opKing.Rank, opKing.Rank - 4);
        int opKingCenterDist = opKingCenterDistFile + opKingCenterDistRank;

        int kingDistFile = Math.Abs(myKing.File - opKing.File);
        int kingDistRank = Math.Abs(myKing.Rank - opKing.Rank);
        int kingDist = 14 - (kingDistFile + kingDistRank);

        currentValue += (kingDist + opKingCenterDist) * 10 + (4100 - opMaterial) / 40;

        currentValue += board.IsInCheck() ? 200 + board.PlyCount * 5 : 0;

        if (isWhite)
        {
            currentValue += opMaterial < 2300 ? (8 - myKing.Rank) * 10 : 0;
        }
        else
        {
            currentValue += opMaterial < 2300 ? (myKing.Rank - 8) * -10 : 0;
        }

        foreach (Piece piece in pawns)
        {
            if (!isWhite)
            {
                currentValue += (piece.Square.Rank - 8);
            }
            else
            {
                currentValue -= (8 - piece.Square.Rank);
            }
        }

        return currentValue;
    }

    public int Negamax(Board board, Timer timer, int depth, int ply, int alpha, int beta)
    {
        bool notRoot = ply > 0,
        qSearch = depth <= 0;
        ulong key = board.ZobristKey;

        if (notRoot && board.IsRepeatedPosition()) return 0;

        TTEntry entry = tt[key % entries];

        if (notRoot && entry.key == key && entry.depth >= depth && (
            entry.bound == 3
                || entry.bound == 2 && entry.score >= beta
                || entry.bound == 1 && entry.score <= alpha
        )) return entry.score;

        Move[] moves = board.GetLegalMoves(qSearch);
        List<int> scoresList = new(),
        valueList = new();
        Move bestMove = Move.NullMove;
        int eval = EvaluatePosition(board),
        maxEval = -99999,
        origAlpha = alpha;

        if (!qSearch && moves.Length == 0) return board.IsInCheck() ? -77777 + ply : 0;

        if (qSearch)
        {
            maxEval = eval;
            if (eval >= beta) { return eval; }
            alpha = Math.Max(alpha, eval);
        }

        foreach (Move scoreMove in moves)
        {
            int currentValue = (int)scoreMove.CapturePieceType * 10 - (int)scoreMove.MovePieceType;
            currentValue += scoreMove.IsPromotion ? (int)scoreMove.PromotionPieceType : 0;
            currentValue += board.IsInCheck() ? (scoreMove.IsCapture ? 200 : 400) : 0;

            if (scoreMove == entry.move) currentValue = 99999;

            scoresList.Add(-currentValue);
        }

        Array.Sort(scoresList.ToArray(), moves);
        if (!notRoot) bestMoveRoot = moves.First();

        foreach (Move searchMove in moves)
        {
            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30) return 99999;

            board.MakeMove(searchMove);
            int callResults = -Negamax(board, timer, depth - 1, ply + 1, -beta, -alpha);
            board.UndoMove(searchMove);
            valueList.Add(callResults);

            if (callResults > maxEval)
            {
                bestMove = searchMove;

                if (!notRoot) bestMoveRoot = bestMove;
            }

            maxEval = Math.Max(maxEval, callResults);
            alpha = Math.Max(alpha, maxEval);

            if (alpha >= beta)
                break;
        }

        int bound = alpha >= beta ? 2 : alpha > origAlpha ? 3 : 1;
        tt[key % entries] = new TTEntry(key, bestMove, depth, maxEval, bound);

        return maxEval;
    }
}