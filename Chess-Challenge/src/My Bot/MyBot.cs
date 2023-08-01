using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
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
        currentValue += PieceType.Pawn == move.CapturePieceType || PieceType.Pawn == move.MovePieceType ?  + board.PlyCount : PieceType.Rook == move.CapturePieceType ? board.PlyCount * 2 : 0;

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

    static int PstValues(Board board, bool isWhite)
    {
        int numPawns = board.GetPieceList(PieceType.Pawn, isWhite).Count;
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues =
        {
            0,
            150 + numPawns*25 + board.PlyCount,
            300,
            400,
            500 + board.PlyCount,
            900,
            10000
        };

        ulong[] psts = { 657614902731556116, 420894446315227099, 384592972471695068, 312245244820264086, 364876803783607569, 366006824779723922, 366006826859316500, 786039115310605588, 421220596516513823, 366011295806342421, 366006826859316436, 366006896669578452, 162218943720801556, 440575073001255824, 657087419459913430, 402634039558223453, 347425219986941203, 365698755348489557, 311382605788951956, 147850316371514514, 329107007234708689, 402598430990222677, 402611905376114006, 329415149680141460, 257053881053295759, 291134268204721362, 492947507967247313, 367159395376767958, 384021229732455700, 384307098409076181, 402035762391246293, 328847661003244824, 365712019230110867, 366002427738801364, 384307168185238804, 347996828560606484, 329692156834174227, 365439338182165780, 386018218798040211, 456959123538409047, 347157285952386452, 365711880701965780, 365997890021704981, 221896035722130452, 384289231362147538, 384307167128540502, 366006826859320596, 366006826876093716, 366002360093332756, 366006824694793492, 347992428333053139, 457508666683233428, 329723156783776785, 329401687190893908, 366002356855326100, 366288301819245844, 329978030930875600, 420621693221156179, 422042614449657239, 384602117564867863, 419505151144195476, 366274972473194070, 329406075454444949, 275354286769374224, 366855645423297932, 329991151972070674, 311105941360174354, 256772197720318995, 365993560693875923, 258219435335676691, 383730812414424149, 384601907111998612, 401758895947998613, 420612834953622999, 402607438610388375, 329978099633296596, 67159620133902 };
        int[] piecePhase = { 0, 0, 1, 1, 2, 4, 0 };

        int mg = 0, eg = 0, phase = 0;

        foreach (bool stm in new[] { true, false })
        {
            for (var p = PieceType.Pawn; p <= PieceType.King; p++)
            {
                int piece = (int)p, ind;
                ulong mask = board.GetPieceBitboard(p, stm);
                while (mask != 0)
                {
                    phase += piecePhase[piece];
                    ind = 128 * (piece - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (stm ? 56 : 0);
                    mg += (int)(((psts[ind / 10] >> (6 * (ind % 10))) & 63) - 20) * 8 + pieceValues[piece];
                    eg += (int)(((psts[(ind + 64) / 10] >> (6 * ((ind + 64) % 10))) & 63) - 20) * 8 + pieceValues[piece];
                }
            }

            mg = -mg;
            eg = -eg;
        }

        return (mg * phase + eg * (24 - phase)) / 24 * (board.IsWhiteToMove ? 1 : -1);
    }
}
