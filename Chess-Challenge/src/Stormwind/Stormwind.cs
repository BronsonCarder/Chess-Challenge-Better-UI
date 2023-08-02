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
    TTEntry[] tt = new TTEntry[entries];

    public Move Think(Board board, Timer timer)
    {
        Move bestMove = Move.NullMove;
        int alpha = -99999;
        int beta = 99999;

        for (int depth = 1; depth <= 42; depth++)
        {
            var results = Negamax(board, timer, depth, 0, alpha, beta, bestMove);

            // Out of time
            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30)
                break;

            bestMove = results.bestMove;
        }

        return bestMove;
    }

    static int GetMaterial(Board board, bool isWhite)
    {
        //Max value 4600
        return board.GetPieceList(PieceType.Pawn, isWhite).Count * 150
            + board.GetPieceList(PieceType.Knight, isWhite).Count * 300
            + board.GetPieceList(PieceType.Bishop, isWhite).Count * 400
            + board.GetPieceList(PieceType.Rook, isWhite).Count * 500
            + board.GetPieceList(PieceType.Queen, isWhite).Count * 1000;
    }

    static int EvaluatePosition(Board board)
    {
        if (board.IsDraw())
            return 0;

        if (board.IsInCheckmate())
            return -99999 + board.PlyCount;

        bool isWhite = board.IsWhiteToMove;
        int myMaterial;
        int opMaterial;

        myMaterial = GetMaterial(board, isWhite);
        opMaterial = GetMaterial(board, !isWhite);
        Square myKing = board.GetKingSquare(isWhite);
        Square opKing = board.GetKingSquare(!isWhite);
        PieceList pawns = board.GetPieceList(PieceType.Pawn, isWhite);

        //Sets current value to the material value of opponent, minus material value of player. 
        int currentValue = myMaterial - opMaterial;

        currentValue += board.IsInCheck() ? 200 + board.PlyCount * 5 : 0;

        if (isWhite)
        {
            currentValue += opMaterial < 2800 ? (8 - myKing.Rank) * 10 : 0;
        }
        else
        {
            currentValue += opMaterial < 2800 ? (myKing.Rank - 8) * -10 : 0;
        }

        foreach (Piece piece in pawns)
        {
            if (!isWhite)
            {
                currentValue += (piece.Square.Rank - 8);
            }
            else
            {
                currentValue += (8 - piece.Square.Rank) * -1;
            }
        }

        int opKingCenterDistFile = Math.Max(3 - opKing.File, opKing.File - 4);
        int opKingCenterDistRank = Math.Max(3 - opKing.Rank, opKing.Rank - 4);
        int opKingCenterDist = opKingCenterDistFile + opKingCenterDistRank;

        int kingDistFile = Math.Abs(myKing.File - opKing.File);
        int kingDistRank = Math.Abs(myKing.Rank - opKing.Rank);
        int kingDist = 14 - (kingDistFile + kingDistRank);

        currentValue += (kingDist + opKingCenterDist) * 10 + (4600 - opMaterial) / 40;

        return currentValue;
    }

    public (int alpha, Move bestMove) Negamax(Board board, Timer timer, int depth, int ply, int alpha, int beta, Move move)
    {
        Move[] moves = board.GetLegalMoves();
        List<int> scoresList = new();
        List<int> valueList = new();
        bool notRoot = ply > 0;
        int maxEval = -99999;
        int origAlpha = alpha;
        ulong key = board.ZobristKey;

        TTEntry entry = tt[key % entries];

        if (notRoot && board.IsDraw())
            return (0, move);

        if (board.IsInCheckmate())
            return (-99999 + board.PlyCount, move);

        if (depth == 0)
        {
            int leafResult = EvaluatePosition(board);
            return (leafResult, move);
        }

        // TT cutoffs
        if (notRoot && entry.key == key && entry.depth >= depth && (
            entry.bound == 3 // exact score
                || entry.bound == 2 && entry.score >= beta // lower bound, fail high
                || entry.bound == 1 && entry.score <= alpha // upper bound, fail low
        )) return (entry.score, entry.move);

        foreach (Move scoreMove in moves)
        {
            int currentValue = (int)scoreMove.CapturePieceType * 10 - (int)scoreMove.MovePieceType;
            currentValue += scoreMove.IsPromotion ? (int)scoreMove.PromotionPieceType : 0;
            if (scoreMove == entry.move) currentValue = 99999;

            scoresList.Add(currentValue);
        }

        Array.Sort(scoresList.ToArray(), moves);
        Array.Reverse(moves);

        foreach (Move searchMove in moves)
        {
            // Out of time
            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30) return (99999, Move.NullMove);

            board.MakeMove(searchMove);
            var callResults = -Negamax(board, timer, depth - 1, ply + 1, -beta, -alpha, move).alpha;
            board.UndoMove(searchMove);
            valueList.Add(callResults);

            maxEval = Math.Max(maxEval, callResults);
            alpha = Math.Max(alpha, maxEval);



            if (alpha >= beta)
                break;
        }

        // Did we fail high/low or get an exact score?
        int bound = maxEval >= beta ? 2 : maxEval > origAlpha ? 3 : 1;
        Move bestMove = moves[valueList.IndexOf(valueList.Max())];

        // Push to TT
        tt[key % entries] = new TTEntry(key, bestMove, depth, maxEval, bound);

        return (alpha, bestMove);
    }
}