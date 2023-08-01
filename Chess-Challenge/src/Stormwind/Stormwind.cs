using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example;
public class Stormwind : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        return Negamax(board, 4, -99999, 99999).bestMove;
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
        if (board.IsInCheckmate())
            return -99999 + board.PlyCount;

        if (board.IsDraw())
            return 0;

        bool isWhite = board.IsWhiteToMove;
        bool isPlayer = board.PlyCount % 2 == 0;
        int myMaterial;
        int opMaterial;

        myMaterial = GetMaterial(board, isWhite);
        opMaterial = GetMaterial(board, !isWhite);
        Square myKing = board.GetKingSquare(isPlayer);
        Square opKing = board.GetKingSquare(!isPlayer);
        PieceList pawns = board.GetPieceList(PieceType.Pawn, isWhite);

        //Sets current value to the material value of opponent, minus material value of player. 
        int currentValue = myMaterial - opMaterial;

        currentValue += board.IsInCheck() ? 200 + board.PlyCount * 5 : 0;

        if (isPlayer)
        {
            currentValue += opMaterial < 2800 ? (8 - myKing.Rank) * 10 : 0;
        }
        else
        {
            currentValue += opMaterial < 2800 ? (myKing.Rank - 8) * -10 : 0;
        }

        foreach (Piece piece in pawns)
        {
            if (!isPlayer)
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

    static (int eval, Move bestMove) Negamax(Board board, int maxDepth, int alpha, int beta)
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

        Move[] moves = board.GetLegalMoves();
        List<int> valueList = new();
        int eval = -99999;

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int callResults = -Negamax(board, maxDepth - 1, -beta, -alpha).eval;
            board.UndoMove(move);
            valueList.Add(callResults);

            eval = Math.Max(eval, callResults);
            alpha = Math.Max(alpha, eval);

            if (alpha >= beta)
                break;
        }

        return (eval, moves[valueList.IndexOf(valueList.Max())]);
    }
}
