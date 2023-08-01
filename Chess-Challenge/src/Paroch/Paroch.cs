using ChessChallenge.API;
using System;
using System.Collections.Generic;

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

    static int EvaluateMove(Board board, Move move, bool isWhite)
    {
        //Check the number of pawns you currently have in play
        int numPawn = board.GetPieceList(PieceType.Pawn, isWhite).Count;
        int numKnight = board.GetPieceList(PieceType.Knight, isWhite).Count;
        int numBishop = board.GetPieceList(PieceType.Bishop, isWhite).Count;
        int numRook = board.GetPieceList(PieceType.Rook, isWhite).Count;
        int numQueen = board.GetPieceList(PieceType.Queen, isWhite).Count;

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

        int material = (numPawn * pieceValues[1]) + (numKnight * pieceValues[2]) + (numBishop * pieceValues[3]) + (numRook * pieceValues[4]) + (numQueen * 1000);
        int inverseMaterialNorm = (4600 - material) / 10;

        //Sets current value to the value of the piece to start out (or 0 for no piece capture), plus some modifiers
        int currentValue = pieceValues[(int)move.CapturePieceType];
        currentValue += PieceType.Pawn == move.CapturePieceType || PieceType.Pawn == move.MovePieceType ? +board.PlyCount : PieceType.Rook == move.CapturePieceType ? board.PlyCount * 2 : 0;

        //This is probably my favorite part of my bot, the DangerValue function. I'll explain in detail when we get there.
        currentValue -= DangerValue(board, move, pieceValues, isWhite);

        //If the piece that's moving is the king, decentivise moving forward, but lay off this as turns pass, can even turn into a benefit for moving the king in late game
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

        board.MakeMove(move);

        //If it's checkmate, we basically just want to do that
        currentValue += board.IsInCheckmate() ? 999999 : 0;

        //If it puts them in check, it gets a bonus, and an extra bonus if that is also a capture
        currentValue += board.IsInCheck() ? (move.IsCapture ? 400 : 200 + inverseMaterialNorm / 4) : 0;

        //And, if it would cause a draw, we disincentivise that, though there's often not a lot you can do about it
        currentValue -= board.IsDraw() ? 99999 : 0;
        board.UndoMove(move);

        return currentValue;
    }

    static int DangerValue(Board board, Move move, int[] pieceValues, bool isWhite)
    {
        int numPassedBefore = NumPassedPawn(board, isWhite);
        int numLegalMovesBefore = board.GetLegalMoves().Length;
        int numLegalAttacksBefore = board.GetLegalMoves(true).Length;

        //Calculate Danger before, make move and calculate Danger after the move
        board.MakeMove(Move.NullMove);
        int dangerBefore = CountDanger(board, pieceValues) + numPassedBefore * 50 + numLegalMovesBefore * 100 + numLegalAttacksBefore * 50;
        board.UndoMove(Move.NullMove);
        board.MakeMove(move);
        int numPassedAfter = NumPassedPawn(board, isWhite);
        int numLegalMovesAfter = board.GetLegalMoves().Length;
        int numLegalAttacksAfter = board.GetLegalMoves(true).Length;
        int dangerAfter = CountDanger(board, pieceValues) + numPassedAfter + numLegalMovesAfter + numLegalAttacksAfter;
        board.UndoMove(move);

        //Subtract the DangerAfter from the Danger before, giving a value that decentivizes putting major pieces in jeopardy
        //But also that incentivizes protecting those same pieces
        int danger = dangerAfter - dangerBefore;

        return danger;
    }

    static int CountDanger(Board board, int[] pieceValues)
    {
        int dangerValue = 0;
        int numAttacks = 0;

        //Since we've already done MakeMove in the larger context, we can just use getlegalmoves to get opponents moves (thanks community!)
        Move[] captureMoves = board.GetLegalMoves(true);

        //Loop through all legal capture moves, get the piece value, update danger value
        for (int i = 0; i < captureMoves.Length; i++)
        {
            int tempValue = pieceValues[(int)captureMoves[i].CapturePieceType];
            dangerValue = Math.Max(dangerValue, tempValue);
            numAttacks++;
        }
        //Add a value representing the number of attacks the opponenet has available
        dangerValue += numAttacks * 50;

        return dangerValue;
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