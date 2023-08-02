using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example;
public class Paroch : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        bool isWhite = board.IsWhiteToMove;
        var valueList = new int[moves.Length];
        List<int> indexList = new();
        int maxMoveValue = int.MinValue;
        Random rng = new();

        //Loop through all legal moves
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];

            //Evaluate the current move and update the current move value, creating a list of values as we go
            int currentValue = EvaluateMove(board, move, isWhite);
            valueList[i] = currentValue;

            if (currentValue > maxMoveValue)
                maxMoveValue = currentValue;
        }

        //Loop through the list of values we made earlier, finding ones that match the moveValue we evaluated and adding them to a list
        for (int i = 0; i < valueList.Length; i++)
        {
            if (valueList[i] == maxMoveValue)
                indexList.Add(i);
        }

        //index is a random index from the list we just created
        int index = rng.Next(indexList.Count);

        int moveToUse = indexList[index]; ;
        Move bestMove = moves[moveToUse];

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

    static int EvaluateMove(Board board, Move move, bool isWhite)
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues =
        {
            0,
            150,
            300,
            400,
            500,
            10000,
            99999
        };

        int material = GetMaterial(board, isWhite);
        int inverseMaterialNorm = (4600 - material) / 10;
        int currentValue = pieceValues[(int)move.CapturePieceType];
        int numPawns = board.GetPieceList(PieceType.Pawn, isWhite).Count;

        board.MakeMove(move);

        //If it's checkmate, we basically just want to do that
        if (board.IsInCheckmate()) { board.UndoMove(move); return 999999; }

        //And, if it would cause a draw... hopefully there's something better we can do. lol
        if (board.IsDraw()) { board.UndoMove(move); return 0; }

        //If it puts them in check, it gets a bonus, and an extra bonus if that is also a capture
        currentValue += board.IsInCheck() ? (move.IsCapture ? 400 : 200 + inverseMaterialNorm / 4) : 0;

        board.UndoMove(move);

        currentValue += PieceType.Pawn == move.CapturePieceType || PieceType.Pawn == move.MovePieceType ? (8 - numPawns) + board.PlyCount : PieceType.Rook == move.CapturePieceType ? board.PlyCount * 2 : 0;

        //This is probably my favorite part of my bot, the DangerValue function. I'll explain in detail when we get there.
        currentValue -= DangerValue(board, move, isWhite);

        //If the piece that's moving is the king, decentivise moving forward,
        //but lay off this as turns pass, can even turn into a benefit for moving the king in late game
        if (move.MovePieceType == PieceType.King && !isWhite && move.StartSquare.Rank > move.TargetSquare.Rank)
        {
            currentValue -= 100 - inverseMaterialNorm / 4;
        }
        else if (move.MovePieceType == PieceType.King && isWhite && move.StartSquare.Rank < move.TargetSquare.Rank)
        {
            currentValue -= 100 - inverseMaterialNorm / 4;
        }

        //If the move is to promote a pawn, promote to queen unless that's a bad move for other reasons
        currentValue += move.IsPromotion ? pieceValues[(int)move.PromotionPieceType] / 100 : 0;

        //If captured piece is passed pawn, incentivize taking it
        currentValue += IsPassedPawn(board, move.TargetSquare, move.CapturePieceType, !isWhite) ? 50 : 0;

        //If move piece is passed pawn, move it forward
        currentValue += IsPassedPawn(board, move.StartSquare, move.MovePieceType, isWhite) ? 100 + board.PlyCount : 0;


        return currentValue;
    }

    static int MVVLVA(Board board)
    {
        Move[] moves = board.GetLegalMoves(true);
        List<int> valueList = new();

        if (moves.Length == 0) return 0;

        int[,] MVV_LVA = new int[7, 7] {
            {0, 0, 0, 0, 0, 0, 0},       // victim None
            {0, 15, 14, 13, 12, 11, 10}, // victim P, attacker None, P, N, B, R, Q, K
            {0, 25, 24, 23, 22, 21, 20}, // victim N, 
            {0, 35, 34, 33, 32, 31, 30}, // victim B, 
            {0, 45, 44, 43, 42, 41, 40}, // victim R,
            {0, 55, 54, 53, 52, 51, 50}, // victim Q, 
            {0, 0, 0, 0, 0, 0, 0}        // victim K
        };

        foreach (Move move in moves)
            valueList.Add(MVV_LVA[(int)move.CapturePieceType, (int)move.MovePieceType]);

        return valueList.Max() * 1000;
    }

    static int DangerValue(Board board, Move move, bool isWhite)
    {
        int numPassedBefore = NumPassedPawn(board, isWhite) * 50;
        int numLegalMovesBefore = board.GetLegalMoves().Length * 100;
        int numLegalAttacksBefore = board.GetLegalMoves(true).Length * 50;

        //Calculate Danger before, make move and calculate Danger after the move
        int dangerBefore = MVVLVA(board) + numPassedBefore + numLegalMovesBefore + numLegalAttacksBefore;

        board.MakeMove(move);
        int numPassedAfter = NumPassedPawn(board, isWhite);
        int numLegalMovesAfter = board.GetLegalMoves().Length;
        int numLegalAttacksAfter = board.GetLegalMoves(true).Length;

        int dangerAfter = MVVLVA(board) + numPassedAfter + numLegalMovesAfter + numLegalAttacksAfter;
        board.UndoMove(move);

        //Subtract the DangerAfter from the Danger before, giving a value that decentivizes putting major pieces in jeopardy
        //But also that incentivizes protecting those same pieces
        int danger = dangerAfter - dangerBefore;

        return danger;
    }

    static bool IsPassedPawn(Board board, Square square, PieceType piece, bool isMaximizing)
    {
        int numPawns = 0;

        //Get the list of all of the opponent's pawns
        PieceList opPieces = board.GetPieceList(PieceType.Pawn, !isMaximizing);

        if (piece == PieceType.Pawn)
        {
            //Loop through all pawns
            for (int i = 0; i < opPieces.Count; i++)
            {
                //If it shares the same rank as the current pawn +/- 1, add it to the count
                if (opPieces[i].Square.Rank >= square.Rank + 1 && opPieces[i].Square.Rank <= square.Rank - 1)
                    numPawns++;
            }

            //If there are no opposing pawns in the space it is a passed pawn
            if (numPawns == 0)
                return true;
        }
        return false;
    }

    static int NumPassedPawn(Board board, bool isWhite)
    {
        int numPassedPawns = 0;

        //Get the list of pawns for your color
        PieceList pieces = board.GetPieceList(PieceType.Pawn, isWhite);

        //Loop over all of the pawns in the list
        for (int i = 0; i < pieces.Count; i++)
        {
            //If it's a passed pawn, count it
            if (IsPassedPawn(board, pieces.GetPiece(i).Square, PieceType.Pawn, isWhite))
                numPassedPawns++;
        }

        return numPassedPawns;
    }
}