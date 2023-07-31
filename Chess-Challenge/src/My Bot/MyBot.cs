using ChessChallenge.API;

using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        return MinMax(board, Move.NullMove, 4, -99999, 99999, true).bestMove;
    }

    static int GetMaterial(Board board, bool isWhite)
    {
        //Check the number of each piece type you have in play
        int numPawn = board.GetPieceList(PieceType.Pawn, isWhite).Count;
        int numKnight = board.GetPieceList(PieceType.Knight, isWhite).Count;
        int numBishop = board.GetPieceList(PieceType.Bishop, isWhite).Count;
        int numRook = board.GetPieceList(PieceType.Rook, isWhite).Count;
        int numQueen = board.GetPieceList(PieceType.Queen, isWhite).Count;

        //Max value 4600
        return numPawn * 150 + numKnight * 300 + numBishop * 400 + numRook * 500 + numQueen * 1000;
    }

    static List<int> EvaluateMoves(Board board)
    {
        List<int> valueList = new();

        if (board.IsInCheckmate())
        {
            valueList.Add(-99999);
            return valueList;
        }

        if (board.IsDraw())
        {
            valueList.Add(0);
            return valueList;
        }

        Move[] moves = board.GetLegalMoves();
        bool isWhite = board.IsWhiteToMove;
        int numPawn = board.GetPieceList(PieceType.Pawn, isWhite).Count;

        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues =
        {
            0,
            150 + (8 - numPawn) * 25 + board.PlyCount,
            300,
            400,
            500 + board.PlyCount * 2,
            10000,
            99999
        };

        foreach (Move move in moves)
        {
            int currentValue = pieceValues[(int)move.CapturePieceType];

            //This is probably my favorite part of my bot, the DangerValue function. I'll explain in detail when we get there.
            currentValue -= DangerValue(board, move, pieceValues);

            //If the move is to promote a pawn, promote to queen unless that's a bad move for other reasons
            currentValue += move.IsPromotion ? pieceValues[(int)move.PromotionPieceType] / 100 : 0;

            valueList.Add(currentValue);
        }

        return valueList;
    }

    static int EvaluatePosition(Board board, bool isWhite)
    {
        if (board.IsInCheckmate())
            return -99999;

        if (board.IsDraw())
            return 0;

        int myMaterial = GetMaterial(board, isWhite);
        int opMaterial = GetMaterial(board, !isWhite);

        //Sets current value to the material value of opponent, minus material value of player. 
        int currentValue = opMaterial - myMaterial;
        int inverseMaterial = 4600 - currentValue;

        currentValue += board.IsInCheck() ? 200 + inverseMaterial / 40 : 0;

        if (board.GetKingSquare(!isWhite).Rank > 3)
        {
            currentValue -= 100 - inverseMaterial / 40;
        }
        else if (board.GetKingSquare(isWhite).Rank < 3)
        {
            currentValue -= 100 - inverseMaterial / 40;
        }

        return currentValue;
    }

    static int DangerValue(Board board, Move move, int[] pieceValues)
    {
        int numLegalMovesBefore = board.GetLegalMoves().Length;
        int numLegalAttacksBefore = board.GetLegalMoves(true).Length;
        //Calculate Danger before, make move and calculate Danger after the move
        board.MakeMove(Move.NullMove);
        int dangerBefore = CountDanger(board, pieceValues);
        board.UndoMove(Move.NullMove);
        board.MakeMove(move);
        int dangerAfter = CountDanger(board, pieceValues);
        int numLegalMovesAfter = board.GetLegalMoves().Length;
        int numLegalAttacksAfter = board.GetLegalMoves(true).Length;
        board.UndoMove(move);

        //Subtract the DangerAfter from the Danger before, giving a value that decentivizes putting major pieces in jeopardy
        //But also that incentivizes protecting those same pieces
        int danger = (numLegalMovesBefore * 100 + numLegalAttacksBefore * 50 + dangerAfter) - (dangerBefore + numLegalMovesAfter + numLegalAttacksAfter);

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

    static (int eval, Move bestMove) MinMax(Board board, Move move, int maxDepth, int alpha, int beta, bool isMaximizing)
    {
        if (board.IsInCheckmate())
            return isMaximizing ? (-99999, Move.NullMove) : (99999, Move.NullMove);

        if (board.IsDraw())
            return (0, Move.NullMove);

        List<int> valueList = new();

        //If you've reached max depth, get the list of the values of all of the moves at that depth
        if (maxDepth == 0)
        {
            int leafResult = EvaluatePosition(board, isMaximizing);
            return (leafResult, Move.NullMove);
        }

        if (move == Move.NullMove)
        {
            valueList = EvaluateMoves(board);
        }
        else
        {
            board.UndoMove(move);
            valueList = EvaluateMoves(board);
            board.MakeMove(move);
        }

        if (isMaximizing)                            //Maximizing
        {
            Move[] moves = board.GetLegalMoves();
            IEnumerable<Move> orderedMoves = moves.Zip(valueList, (move, value) => new { Move = move, Value = value })
                                     .OrderByDescending(item => item.Value)
                                     .Select(item => item.Move)
                                     .ToList();
            valueList = new();
            int maxEval = -99999;

            foreach (Move maxMove in orderedMoves)
            {
                board.MakeMove(maxMove);

               // if (cache.contains(new State(board, maxDepth, isMaximizing))
                      //  return cache.get(new State(board, maxDepth, isMaximizing));

                //Call this function, starting this process from the beginning, but with maxDepth - 1
                int callResults = -MinMax(board, maxMove, maxDepth - 1, alpha, beta, false).eval;
                board.UndoMove(maxMove);
                valueList.Add(callResults);

                maxEval = Math.Max(maxEval, callResults);
                alpha = Math.Max(alpha, maxEval);

                if (alpha >= beta)
                    break;
            }

            //Cache.Put(new State(board, maxDepth, isMaximizing));
            return (maxEval, orderedMoves.First());
        }
        else                                       //Minimizing
        {
            Move[] moves = board.GetLegalMoves();
            IEnumerable<Move> orderedMoves = moves.Zip(valueList, (move, value) => new { Move = move, Value = value })
                                     .OrderByDescending(item => item.Value)
                                     .Select(item => item.Move)
                                     .ToList();
            valueList = new();
            int minEval = 99999;

            foreach (Move minMove in orderedMoves)
            {
                board.MakeMove(minMove);

                //Call this function, starting this process from the beginning, but with maxDepth - 1
                int callResults = -MinMax(board, minMove, maxDepth - 1, alpha, beta, true).eval;
                board.UndoMove(minMove);
                valueList.Add(callResults);

                minEval = Math.Min(minEval, callResults);
                beta = Math.Min(beta, minEval);

                if (alpha >= beta)
                    break;
            }

            return (minEval, orderedMoves.Last());
        }
    }
}
