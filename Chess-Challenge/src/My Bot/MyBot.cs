using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        Move bestMove = Move.NullMove;
        int alpha = -99999;
        int beta = 99999;

        for (int depth = 1; depth <= 10; depth++)
        {
            var results = Negamax(board, timer, depth, alpha, beta);

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

    static Move[] MoveOrdering(Board board)
    {
        Move[] moves = board.GetLegalMoves();
        List<int> valueList = new();

        foreach (Move move in moves)
            valueList.Add(-((int)move.CapturePieceType * 100 - (int)move.MovePieceType));

        Array.Sort(valueList.ToArray(), moves);
        return moves;
    }

    static (int alpha, Move bestMove) Negamax(Board board, Timer timer, int maxDepth, int alpha, int beta)
    {
        if (board.IsInCheckmate())
            return (-99999 + board.PlyCount, Move.NullMove);

        if (board.IsDraw())
            return (0, Move.NullMove);

        if (maxDepth == 0)
        {
            int leafResult = EvaluatePosition(board);
            return (leafResult, Move.NullMove);
        }

        Move[] moves = MoveOrdering(board);
        List<int> valueList = new();
        int maxEval = -99999;

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            var callResults = -Negamax(board, timer, maxDepth - 1, -beta, -alpha).alpha;
            board.UndoMove(move);
            valueList.Add(callResults);

            maxEval = Math.Max(maxEval, callResults);
            alpha = Math.Max(alpha, maxEval);

            // Out of time
            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30)
                break;

            if (alpha >= beta)
                break;
        }

        return (alpha, moves[valueList.IndexOf(valueList.Max())]);
    }
}