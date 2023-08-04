using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        return MinMax(board, 4, -99999, 99999, true).bestMove;
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

    static int EvaluatePosition(Board board, bool isMaximizing)
    {
        if (board.IsInCheckmate())
            return isMaximizing ? -99999 + board.PlyCount : 99999 - board.PlyCount;

        if (board.IsDraw())
            return 0;

        bool isWhite = board.IsWhiteToMove;
        int myMaterial;
        int opMaterial;

        myMaterial = GetMaterial(board, isWhite);
        opMaterial = GetMaterial(board, !isWhite);
        Square myKing = board.GetKingSquare(isMaximizing);
        Square opKing = board.GetKingSquare(!isMaximizing);
        PieceList pawns = board.GetPieceList(PieceType.Pawn, isWhite);

        //Sets current value to the material value of opponent, minus material value of player. 
        int currentValue = myMaterial - opMaterial;

        currentValue += board.IsInCheck() ? 200 + board.PlyCount * 5 : 0;

        if (isMaximizing)
        {
            currentValue += opMaterial < 2800 ? (8 - myKing.Rank) * 10 : 0;
        }
        else
        {
            currentValue += opMaterial < 2800 ? (myKing.Rank - 8) * -10 : 0;
        }

        foreach (Piece piece in pawns)
        {
            if (!isMaximizing)
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

    static (int eval, Move bestMove) MinMax(Board board, int maxDepth, int alpha, int beta, bool isMaximizing)
    {
        if (board.IsInCheckmate())
            return isMaximizing ? (-99999 + board.PlyCount, Move.NullMove) : (99999 - board.PlyCount, Move.NullMove);

        if (board.IsDraw())
            return (0, Move.NullMove);

        //If you've reached max depth, get the list of the values of all of the moves at that depth
        if (maxDepth == 0)
        {
            int leafResult = EvaluatePosition(board, isMaximizing);
            return (leafResult, Move.NullMove);
        }

        if (isMaximizing)                            //Maximizing
        {
            Move[] moves = board.GetLegalMoves();
            List<int> valueList = new();
            int maxEval = -99999;

            foreach (Move maxMove in moves)
            {
                board.MakeMove(maxMove);

                //Call this function, starting this process from the beginning, but with maxDepth - 1
                int callResults = MinMax(board, maxDepth - 1, alpha, beta, false).eval;
                board.UndoMove(maxMove);
                valueList.Add(callResults);

                maxEval = Math.Max(maxEval, callResults);
                alpha = Math.Max(alpha, maxEval);

                if (alpha >= beta)
                    break;
            }

            return (maxEval, moves[valueList.IndexOf(valueList.Max())]);
        }
        else                                       //Minimizing
        {
            Move[] moves = board.GetLegalMoves();
            List<int> valueList = new();
            int minEval = 99999;

            foreach (Move minMove in moves)
            {
                board.MakeMove(minMove);

                //Call this function, starting this process from the beginning, but with maxDepth - 1
                int callResults = MinMax(board, maxDepth - 1, alpha, beta, true).eval;
                board.UndoMove(minMove);
                valueList.Add(callResults);

                minEval = Math.Min(minEval, callResults);
                beta = Math.Min(beta, minEval);

                if (alpha >= beta)
                    break;
            }

            return (minEval, moves[valueList.IndexOf(valueList.Min())]);
        }
    }
}