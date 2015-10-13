﻿
using Reversi.Persistence;

using System;
using System.Threading.Tasks;
using System.Timers;

using System.Windows.Forms;

namespace Reversi.Model
{
    /// <summary>
    /// Type of the Reversi game.
    /// </summary>
    public class ReversiGameModel
    {

        #region Constant Default Values

        /// <summary>
        ///The default table size. It is readonly.
        /// </summary>
        private readonly Int32 _tableSizeSettingDefault;

        #endregion

        #region Fields

        /// <summary>
        /// The interface that we get form view. We need to implement this.
        /// </summary>
        private IReversiDataAccess _dataAccess;

        /// <summary>
        /// All the data we need for saving or loading a game.
        /// The put downs coordinates in order, the players times and the size of the table.
        /// </summary>
        private ReversiGameDescriptiveData _data;

        //TODO: Is it a state 4 possible?
        /// <summary>
        /// The table itself. Its values can be -1, 0, 1, 2, 3, 4, 5, 6.
        /// The 0 means it is uncharted field. No put downs even near.
        /// The -1 means it is a player 1 put down field.
        /// The 1 means it is a player 2 put down field.
        /// The 3 means it is a field, where player 2 can possible put down.
        /// The 4 means it is a field, where both player can possible put down.
        /// The 5 means it is a field, that is a candidate to be a possible put down in the next turn.
        /// The 6 means it is a field, where player 1 can possible put down.
        /// If we out of range it returns -2. We only check it if we are not sure.
        /// </summary>
        private Int32[,] _table;

        /// <summary>
        /// Indicate which player turn it is.
        /// If it is true it is player 1's turn.
        /// If it is false it is player 2's turn.
        /// </summary>
        private Boolean _isPlayer1TurnOn;
        /// <summary>
        /// Indicate if it is a passing turn.
        /// </summary>
        private Boolean _isPassingTurnOn;

        /// <summary>
        /// We save the player 1 points at index 0.
        /// We save the remaining put down positions at index 1.
        /// We save the player 2 points at index 2.
        /// </summary>
        private Int32[] _points;

        /// <summary>
        /// We use this array for saving the possible put downs coordinates.
        /// </summary>
        private Int32[] _possiblePutDowns;
        /// <summary>
        /// It is a helper variable, for maintain the '_possiblePutDowns' array.
        /// </summary>
        private Int32 _possiblePutDownsSize;
        /// <summary>
        /// Using it for updating the view.
        /// </summary>
        private Int32 _newPossiblePutDownsCountx3;

        /// <summary>
        /// We save the reversed put downs coordiantes in it.
        /// </summary>
        private Int32[] _reversedPutDowns;
        /// <summary>
        /// It is a helper variable, for maintain the '_reversedPutDowns' array.
        /// </summary>
        private Int32 _reversedPutDownsSize;

        /// <summary>
        /// The timer which, invoke the Timer_Elapsed event. This helps count the players time.
        /// </summary>
        private System.Timers.Timer _timer;
        /// <summary>
        /// A bool for know that if at least one time a NewGame or the Load functions were called.
        /// So we know that we had called InicializeFields.
        /// </summary>
        private Boolean _isGameStarted;

        /// <summary>
        /// we save the user choosen table size for the next NewGame.
        /// </summary>
        private Int32 _tableSizeSetting;
        /// <summary>
        /// We save the table size, that we started to play on, so the view can know.
        /// </summary>
        private Int32 _activeTableSize;

        /// <summary>
        /// Helper delegate array for searching from one point on the table to a direction.
        /// </summary>
        private Direction[] _allDirections;
        /// <summary>
        /// Helper delegate array for searching from one point on the table to a reversed direction.
        /// </summary>
        private Direction[] _allReversedDirections;

        #endregion

        #region Properties

        /// <summary>
        /// The property that the view will use for setting the table size for the model, which will use it at a NewGame.
        /// </summary>
        public Int32 TableSizeSetting
        {
            get
            {
                return _tableSizeSetting;
            }
            set
            {
                _tableSizeSetting = value;
            }
        }

        /// <summary>
        /// The property that the view will use for getting the table size for the active game.
        /// </summary>
        public Int32 ActiveTableSize
        {
            get
            {
                return _activeTableSize;
            }
        }

        #endregion

        #region Delegates

        /// <summary>
        /// Delegate that get the coordinates of the new position from the given coordinates.
        /// </summary>
        /// <param name="x">The first coordinate.</param>
        /// <param name="y">The second coordinate.</param>
        private delegate void Direction(ref Int32 x, ref Int32 y);

        #endregion

        #region Events

        /// <summary>
        /// An in game second passed (no pause), we need to update one of the person's time on the view.
        /// </summary>
        public event EventHandler<ReversiUpdatePlayerTimeEventArgs> UpdatePlayerTime;

        /// <summary>
        /// The game table changed, we will update it on the view.
        /// </summary>
        public event EventHandler<ReversiUpdateTableEventArgs> UpdateTable;

        /// <summary>
        /// The game ended, we need to tell it to the view.
        /// </summary>
        public event EventHandler<ReversiSetGameEndedEventArgs> SetGameEnded;

        #endregion

        #region Constructors

        /// <summary>
        /// The Reversi game model constructor. It dose not generate a game.
        /// </summary>
        /// <param name="dataAccess">The data access.</param>
        /// <param name="defaultGameTableSizes">The default game size.</param>
        public ReversiGameModel(IReversiDataAccess dataAccess, Int32 defaultGameTableSizes)
        {
            _tableSizeSettingDefault = defaultGameTableSizes;

            _dataAccess = dataAccess;
            _tableSizeSetting = _tableSizeSettingDefault;
            _isGameStarted = false;

            _timer = new System.Timers.Timer(1000.0); // It will invoke every 1 second.
            _timer.Elapsed += Timer_Elapsed; // It will invoke Timer_Elapsed private method

            _allDirections = new Direction[] { ToUp, ToRightUp, ToRight, ToRightDown, ToDown, ToLeftDown, ToLeft, ToLeftUp };
            _allReversedDirections = new Direction[] { ToDown, ToLeftDown, ToLeft, ToLeftUp, ToUp, ToRightUp, ToRight, ToRightDown };
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Creating new reversi game with the presetted table size.
        /// </summary>
        public void NewGame()
        {
            _timer.Enabled = false;

            _activeTableSize = _tableSizeSetting;
            _data = new ReversiGameDescriptiveData(_activeTableSize);

            InitializeFields(false);

            _timer.Enabled = true;
        }

        /// <summary>
        /// Loading a reversi game. We check if it is valid or not while setting up the game table.
        /// </summary>
        /// <param name="path">The path to the file, that contains the saved game data.</param>
        public async Task LoadGame(String path)
        {
            _timer.Enabled = false;

            _data = await _dataAccess.Load(path);
            _activeTableSize = _data.TableSize;

            InitializeFields(true);

            _timer.Enabled = true;
        }

        /// <summary>
        /// Saving the reversi game. It will be a valid save.
        /// </summary>
        /// <param name="path">The path to the file, that we will save our actual game data.</param>
        public async Task SaveGame(String path)
        {
            _timer.Enabled = false;

            await _dataAccess.Save(path, _data);

            _timer.Enabled = true;
        }

        /// <summary>
        /// Make a put down by the parameter coordinates. The view sent these, so we will check if they are valid coordinates.
        /// </summary>
        /// <param name="x">The first coordinate of the put down position.</param>
        /// <param name="y">The second coordinate of the put down position.</param>
        public void PutDown(Int32 x, Int32 y)
        {
            if (_isGameStarted && _timer.Enabled && IsValidIndexes(x, y))
            {
                if (MakePutDown(x, y))
                {
                    _timer.Enabled = false;
                    OnSetGameEnded(new ReversiSetGameEndedEventArgs(_points[0], _points[2]));
                }
            }
        }

        /// <summary>
        /// Stop the game. The view uses it.
        /// </summary>
        public void GamePause()
        {
            if (_isGameStarted)
            {
                _timer.Enabled = false;
            }
        }

        /// <summary>
        /// We start the game again. The view uses it.
        /// </summary>
        public void GameUnpause()
        {
            if (_isGameStarted)
            {
                _timer.Enabled = true;
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// First reset the variables to the starting values, then if we loaded the game we replay it from the read data.
        /// While we are building the table we are checking if it is valid. After that send the updated table values to the view.
        /// </summary>
        /// <param name="isLoadedGame">True if we inicialize for a load game. False if we inicialize for a new game.</param>
        private void InitializeFields(Boolean isLoadedGame)
        {
            if (_table == null || _data.TableSize != _table.GetLength(0))
            {
                _table = new Int32[_data.TableSize, _data.TableSize];
                _possiblePutDowns = new Int32[_data.TableSize * _data.TableSize * 2]; //TODO: It can be smaller. How much?
            }

            for (Int32 x = 0; x < _data.TableSize; ++x)
            {
                for (Int32 y = 0; y < _data.TableSize; ++y)
                {
                    // We clear the table.
                    _table[x, y] = 0;
                }
            }

            _isPlayer1TurnOn = true;
            _isPassingTurnOn = false;

            // The 12 * 2 size for the 12 starting possible put down coordinates, pluss 12 for the values.
            _possiblePutDownsSize = 36;

            Int32 halfTableSize = (_data.TableSize / 2);

            // The starting put downs for player 1.
            _table[halfTableSize - 1, halfTableSize - 1] = -1;
            _table[halfTableSize, halfTableSize] = -1;

            // The starting put downs for player 2.
            _table[halfTableSize - 1, halfTableSize] = 1;
            _table[halfTableSize, halfTableSize - 1] = 1;

            // The possible put down coordinates around the starting points.
            _table[halfTableSize - 2, halfTableSize - 2] = 5;
            _possiblePutDowns[0] = halfTableSize - 2;
            _possiblePutDowns[1] = halfTableSize - 2;
            _possiblePutDowns[2] = 5;

            _table[halfTableSize - 1, halfTableSize - 2] = 3;
            _possiblePutDowns[3] = halfTableSize - 1;
            _possiblePutDowns[4] = halfTableSize - 2;
            _possiblePutDowns[5] = 3;

            _table[halfTableSize, halfTableSize - 2] = 6;
            _possiblePutDowns[6] = halfTableSize;
            _possiblePutDowns[7] = halfTableSize - 2;
            _possiblePutDowns[8] = 6;

            _table[halfTableSize + 1, halfTableSize - 2] = 5;
            _possiblePutDowns[9] = halfTableSize + 1;
            _possiblePutDowns[10] = halfTableSize - 2;
            _possiblePutDowns[11] = 5;

            _table[halfTableSize + 1, halfTableSize - 1] = 6;
            _possiblePutDowns[12] = halfTableSize + 1;
            _possiblePutDowns[13] = halfTableSize - 1;
            _possiblePutDowns[14] = 6;

            _table[halfTableSize + 1, halfTableSize] = 3;
            _possiblePutDowns[15] = halfTableSize + 1;
            _possiblePutDowns[16] = halfTableSize;
            _possiblePutDowns[17] = 3;

            _table[halfTableSize + 1, halfTableSize + 1] = 5;
            _possiblePutDowns[18] = halfTableSize + 1;
            _possiblePutDowns[19] = halfTableSize + 1;
            _possiblePutDowns[20] = 5;

            _table[halfTableSize, halfTableSize + 1] = 3;
            _possiblePutDowns[21] = halfTableSize;
            _possiblePutDowns[22] = halfTableSize + 1;
            _possiblePutDowns[23] = 3;

            _table[halfTableSize - 1, halfTableSize + 1] = 6;
            _possiblePutDowns[24] = halfTableSize - 1;
            _possiblePutDowns[25] = halfTableSize + 1;
            _possiblePutDowns[26] = 6;

            _table[halfTableSize - 2, halfTableSize + 1] = 5;
            _possiblePutDowns[27] = halfTableSize - 2;
            _possiblePutDowns[28] = halfTableSize + 1;
            _possiblePutDowns[29] = 5;

            _table[halfTableSize - 2, halfTableSize] = 6;
            _possiblePutDowns[30] = halfTableSize - 2;
            _possiblePutDowns[31] = halfTableSize;
            _possiblePutDowns[32] = 6;

            _table[halfTableSize - 2, halfTableSize - 1] = 3;
            _possiblePutDowns[33] = halfTableSize - 2;
            _possiblePutDowns[34] = halfTableSize - 1;
            _possiblePutDowns[35] = 3;

            // The staring points of the players, and the remaining empty positions.
            _points = new Int32[3] { 2, (_data.TableSize * _data.TableSize) - 4, 2 };

            // We loaded the game.
            if (isLoadedGame)
            {
                // We replay the game to see, if it is a valid one, and to update the model fields. 
                for (Int32 i = 0; i < _data.PutDownsCoordinatesCount; i += 2)
                {
                    // Corrupt loaded data.
                    if (!IsValidIndexes(_data[i], _data[i + 1]))
                    {
                        throw new ReversiDataException("asd10", "as122d", ReversiDataExceptionType.FormatException);
                    }

                    // Make the put down.
                    if (MakePutDown(_data[i], _data[i + 1], false))
                    {
                        throw new ReversiDataException("123asd", "123asd", ReversiDataExceptionType.FormatException);
                    }
                }
            }

            // Geather and send the table values to view.
            Int32[] updatedFieldsDatas = new Int32[_data.TableSize * _data.TableSize];

            for (Int32 x = 0; x < _data.TableSize; ++x)
            {
                for (Int32 y = 0; y < _data.TableSize; ++y)
                {
                    updatedFieldsDatas[(x * _data.TableSize) + y] = _table[x, y];
                }
            }

            _reversedPutDownsSize = 0;
            _reversedPutDowns = new Int32[(_data.TableSize * 12) - 39];

            OnUpdateTable(new ReversiUpdateTableEventArgs(0, updatedFieldsDatas, _points[0], _points[2], _isPassingTurnOn));
            
            // We started at least one game.
            _isGameStarted = true;
        }

        /// <summary>
        /// The actual code for make a put down on the table.
        /// </summary>
        /// <param name="x">The first coordinate of the origin of the put down.</param>
        /// <param name="y">The second coordinate of the origin of the put down.</param>
        /// <param name="isUpdateNeeded">Do we need to send the new values to the view?</param>
        /// <returns>Is the game over?</returns>
        private Boolean MakePutDown(Int32 x, Int32 y, Boolean isUpdateNeeded = true)
        {
            Help2();
            // Updating the table put downs positions. 
            if (_isPlayer1TurnOn) // Player 1 put down.
            {
                // Do we try to make a valid put down? We only check it if loaded the game.
                if (isUpdateNeeded || _table[x, y] == 4 || _table[x, y] == 6) 
                {
                    _table[x, y] = 1; // The put down.

                    for (Int32 i = 0; i < _allDirections.GetLength(0); ++i) // We do the reverses.
                    {
                        SearchAndReverse(x, y, _allDirections[i], _allReversedDirections[i]);
                    }
                }
                else
                {
                    throw new ReversiDataException("Source 01", "message 01", ReversiDataExceptionType.FormatException);
                }
            }
            else // Player 2 put down.
            {
                // Do we try to make a valid put down? We only check it if loaded the game.
                if (isUpdateNeeded || _table[x, y] == 4 || _table[x, y] == 3)
                {
                    _table[x, y] = 2; // The put down.

                    for (Int32 i = 0; i < _allDirections.GetLength(0); ++i) // We do the reverses.
                    {
                        SearchAndReverse(x, y, _allDirections[i], _allReversedDirections[i]);
                    }
                }
                else
                {
                    throw new ReversiDataException("Source 02", "message 02", ReversiDataExceptionType.FormatException);
                }
            }
            Help2();
            // Updating the table old possible put downs positions and remove the one that was played on.
            for (Int32 i = 0; i < _possiblePutDownsSize; i += 3)
            {
                if (_table[_possiblePutDowns[i], _possiblePutDowns[i + 1]] == 1
                    || _table[_possiblePutDowns[i], _possiblePutDowns[i + 1]] == -1)
                {
                    _possiblePutDowns[i] = _possiblePutDowns[_possiblePutDownsSize - 3];
                    _possiblePutDowns[i + 1] = _possiblePutDowns[_possiblePutDownsSize - 2];
                    _possiblePutDowns[i + 2] = _possiblePutDowns[_possiblePutDownsSize - 1];
                    _possiblePutDownsSize -= 3;
                }
                else
                {
                    _table[_possiblePutDowns[i], _possiblePutDowns[i + 1]] = 5;
                }

                for (Int32 j = 0; j < _allDirections.GetLength(0); ++j)
                {
                    SearchAndSetPossiblePutDown(_possiblePutDowns[i], _possiblePutDowns[i + 1], _allDirections[j]);
                }
            }

            _newPossiblePutDownsCountx3 = 0;
            Help2();
            // Updating the table new possible put down positions, and add them to the end of the array.
            for (Int32 i = 0; i < _allDirections.GetLength(0); ++i)
            {
                if (SearchAndAddThenSetPossiblePutDown(x, y, _allDirections[i]))
                {
                    Int32 xNew = x;
                    Int32 yNew = y;
                    _allDirections[i](ref xNew, ref yNew);
                    _possiblePutDowns[_possiblePutDownsSize] = xNew;
                    _possiblePutDowns[_possiblePutDownsSize + 1] = yNew;
                    _possiblePutDowns[_possiblePutDownsSize + 2] = _table[xNew, yNew];
                    _possiblePutDownsSize += 3;
                    _newPossiblePutDownsCountx3 += 3;
                }
            }

            Boolean isOver = false;
            _isPassingTurnOn = true;
            Help2();
            // Change the aktív player and the passing turn Boolean, if the other player can make a put down. It is over, if none can make a put donwn.
            for (Int32 i = 0; i < _possiblePutDownsSize; i += 3)
            {
                if (_isPlayer1TurnOn
                    && (_table[_possiblePutDowns[i], _possiblePutDowns[i + 1]] == 4
                    || _table[_possiblePutDowns[i], _possiblePutDowns[i + 1]] == 3))
                {
                    _isPlayer1TurnOn = !_isPlayer1TurnOn;
                    _isPassingTurnOn = false;
                    isOver = false;
                    break;
                }
                else if (!_isPlayer1TurnOn
                    && (_table[_possiblePutDowns[i], _possiblePutDowns[i + 1]] == 4
                    || _table[_possiblePutDowns[i], _possiblePutDowns[i + 1]] == 6))
                {
                    _isPlayer1TurnOn = !_isPlayer1TurnOn;
                    _isPassingTurnOn = false;
                    isOver = false;
                    break;
                }
            }

            if (isUpdateNeeded) // We harvest the changed coordinates and values, from '_possiblePutDowns' and '_reversedPutDowns'.
            {
                Int32 updatedFieldsDatasSize = 0;
                Int32 changedPossiblePutDownPositionsCountx3 = 0;
                Int32 oldPassiblePutDownsCountx3 = _possiblePutDownsSize - _newPossiblePutDownsCountx3;

                // Check if the value of the possible put downs changed.
                for (Int32 i = 0; i < oldPassiblePutDownsCountx3; i += 3)
                {
                    if (_possiblePutDowns[i + 2] != _table[_possiblePutDowns[i], _possiblePutDowns[i + 1]])
                    {
                        changedPossiblePutDownPositionsCountx3 += 3;
                    }
                }

                updatedFieldsDatasSize = changedPossiblePutDownPositionsCountx3 + _newPossiblePutDownsCountx3 + _reversedPutDownsSize + 3;

                Int32[] updatedFieldsDatas = new Int32[updatedFieldsDatasSize];

                Int32 index = 0;
                for (Int32 i = 0; i < oldPassiblePutDownsCountx3; i += 3)
                {
                    if (_possiblePutDowns[i + 2] != _table[_possiblePutDowns[i], _possiblePutDowns[i + 1]])
                    {
                        _possiblePutDowns[i + 2] = _table[_possiblePutDowns[i], _possiblePutDowns[i + 1]];
                        updatedFieldsDatas[index] = _possiblePutDowns[i];
                        updatedFieldsDatas[index + 1] = _possiblePutDowns[i + 1];
                        updatedFieldsDatas[index + 2] = _possiblePutDowns[i + 2];
                        index += 3;
                    }
                }

                for (Int32 i = oldPassiblePutDownsCountx3; i < _possiblePutDownsSize; i += 3)
                {
                    updatedFieldsDatas[index] = _possiblePutDowns[i];
                    updatedFieldsDatas[index + 1] = _possiblePutDowns[i + 1];
                    updatedFieldsDatas[index + 2] = _possiblePutDowns[i + 2];
                    index += 3;
                }

                for (Int32 i = 0; i < _reversedPutDownsSize; i += 3)
                {
                    updatedFieldsDatas[index + i] = _reversedPutDowns[i];
                    updatedFieldsDatas[index + i + 1] = _reversedPutDowns[i + 1];
                    updatedFieldsDatas[index + i + 2] = _reversedPutDowns[i + 2];
                    index += 3;
                }

                updatedFieldsDatas[index] = x;
                updatedFieldsDatas[index + 1] = y;
                updatedFieldsDatas[index + 2] = _table[x, y];

                // Save the put down.
                _data[_data.PutDownsCoordinatesCount] = x;
                _data[_data.PutDownsCoordinatesCount + 1] = y;
                _data.PutDownsCoordinatesCount += 2;
                Help2();
                // Make the view update call.
                OnUpdateTable(new ReversiUpdateTableEventArgs(updatedFieldsDatasSize, updatedFieldsDatas, _points[0], _points[2], _isPassingTurnOn));

                // Reset for the next put down.
                _reversedPutDownsSize = 0;
            }

            // Is the game over?
            if (isOver) 
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Search if there is any put downs to reverse in one direction, and revers them,
        /// then save coordinates for the possible view update.
        /// </summary>
        /// <param name="xFrom">The first coordinate of the origin of the search.</param>
        /// <param name="yFrom">The second coordinate of the origin of the search.</param>
        /// <param name="direction">The direction where we start the search.</param>
        /// <param name="reversedDirection">The riverse of the parameter direction for step back.</param>
        private void SearchAndReverse(Int32 xFrom, Int32 yFrom, Direction direction, Direction reversedDirection)
        {
            Int32 valueOriginal = _table[xFrom, yFrom];
            Int32 xOriginal = xFrom;
            Int32 yOriginal = yFrom;

            // Step to the direction.
            direction(ref xFrom, ref yFrom);

            // Only interested if the searched position have the inverz value of the original position.
            if (GetSearchValue(ref xFrom, ref yFrom) == valueOriginal * -1)
            {
                // Step to the direction.
                direction(ref xFrom, ref yFrom);
                while (true)
                {
                    Int32 valueSearch = GetSearchValue(ref xFrom, ref yFrom);

                    if (valueSearch == valueOriginal) // We found the put down, that same us the original.
                    {
                        // We step back.
                        reversedDirection(ref xFrom, ref yFrom);

                        // We reverse all the put downs, we find between here and origin.
                        while (GetSearchValue(ref xFrom, ref yFrom) == valueOriginal * -1)
                        {
                            _table[xFrom, yFrom] = valueOriginal;
                            _reversedPutDowns[_reversedPutDownsSize] = xFrom;
                            _reversedPutDowns[_reversedPutDownsSize + 1] = yFrom;
                            _reversedPutDowns[_reversedPutDownsSize + 2] = valueOriginal;
                            _reversedPutDownsSize += 3;

                            ++(_points[valueOriginal + 1]);
                            --(_points[(valueOriginal * -1) + 1]);
                            --_points[1];
                            reversedDirection(ref xFrom, ref yFrom);
                        }
                        return;
                    }
                    else if (valueSearch == valueOriginal * -1) // We find an inverz value of the original position.
                    {
                        // We step to the direction.
                        direction(ref xFrom, ref yFrom);
                    }
                    else
                    {
                        // We run out of the table or we find a possible put down position.
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xFrom">The first coordinate of the origin of the search.</param>
        /// <param name="yFrom">the second coordinate of the origin of the search.</param>
        /// <param name="direction">>The direction where we start the search.</param>
        private void SearchAndSetPossiblePutDown(Int32 xFrom, Int32 yFrom, Direction direction)
        {
            Int32 valueOriginal = _table[xFrom, yFrom];
            Int32 xOriginal = xFrom;
            Int32 yOriginal = yFrom;

            if (valueOriginal == 4) // If we already set to 4 (both can put down here), we dont need to check for anything else.
            {
                return;
            }

            // Make a step.
            direction(ref xFrom, ref yFrom);
            if (GetSearchValue(ref xFrom, ref yFrom) == -1) // Player 1 put down found.
            {
                if (valueOriginal == 3) //  The original was player 2 possible put down.
                {
                    return;
                }

                // Make a step.
                direction(ref xFrom, ref yFrom);
                while (true)
                {
                    Int32 valueSearch = GetSearchValue(ref xFrom, ref yFrom);
                    if (valueSearch == 1) // Possible put down.
                    {
                        // If it was 5 (neither can put here), it will be 3 (player 2 possible put down).
                        // If it was 6 (player 1 possible put down), it will be 4 (both can put down).
                        _table[xOriginal, yOriginal] -= 2;
                        return;
                    }
                    else if (valueSearch == -1) // It still can be a possible put down.
                    {
                        direction(ref xFrom, ref yFrom);
                    }
                    else
                    {
                        return;
                    }
                }
            }
            else if (GetSearchValue(ref xFrom, ref yFrom) == 1) // Player 2 put down found.
            {
                if (valueOriginal == 6) // The original was player 1 possible put down.
                {
                    return;
                }

                // Make a step.
                direction(ref xFrom, ref yFrom);
                while (true)
                {
                    Int32 valueSearch = GetSearchValue(ref xFrom, ref yFrom);
                    if (valueSearch == -1) // Possible put down.
                    {
                        // If it was 5 (neither can put here), it will be 6 (player 1 possible put down).
                        // If it was 3 (player 2 possible put down), it will be 4 (both can put down).
                        ++(_table[xOriginal, yOriginal]);
                        return;
                    }
                    else if (valueSearch == 1) // It still can be a possible put down.
                    {
                        direction(ref xFrom, ref yFrom);
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Search for the new possible put downs. If we find one we set them.
        /// </summary>
        /// <param name="xFrom">The first coordinate of the origin of the search.</param>
        /// <param name="yFrom">The second coordinate of the origin of the search.</param>
        /// <param name="direction">The direction, where we start the search.</param>
        /// <returns>Did we find a possible put down position in this direction?</returns>
        private Boolean SearchAndAddThenSetPossiblePutDown(Int32 xFrom, Int32 yFrom, Direction direction)
        {
            // Make a step.
            direction(ref xFrom, ref yFrom);
            if (GetSearchValue(ref xFrom, ref yFrom) == 0) // Found a new possible put down position.
            {
                _table[xFrom, yFrom] = 5; // So far it is niether player's possible put down position.
                for (Int32 i = 0; i < _allDirections.GetLength(0); ++i) // Set it.
                {
                    SearchAndSetPossiblePutDown(xFrom, yFrom, _allDirections[i]);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the value of the table position at these coordinates given as parameters.
        /// If it is out of the table we return -2, otherwise the value.
        /// We use this when we search on the table.
        /// </summary>
        /// <param name="x">The first coordinate of the position.</param>
        /// <param name="y">The second coordinate of the position.</param>
        /// <returns>The value of the table or -2.</returns>
        private Int32 GetSearchValue(ref Int32 x, ref Int32 y)
        {
            if (x < 0 || x >= _data.TableSize || y < 0 || y >= _data.TableSize)
            {
                return -2;
            }

            return _table[x, y];
        }

        /// <summary>
        /// We check if the table position at these coordinates given as parameters are valid.
        /// We use it when we get position from the view or from the loaded data.
        /// </summary>
        /// <param name="x">The first coordinate of the position.</param>
        /// <param name="y">The second coordinate of the position.</param>
        /// <returns>True if it is a valid position, false otherwise.</returns>
        private Boolean IsValidIndexes(Int32 x, Int32 y)
        {
            if (x < 0 || x >= _data.TableSize || y < 0 || y >= _data.TableSize)
            {
                return false;
            }

            return true;
        }

        #region Private delegates methods

        /// <summary>
        /// Set the parameter coordinates to a new position.
        /// </summary>
        /// <param name="x">The first coordinate of the original position.</param>
        /// <param name="y">The second coordinate of the original position.</param>
        private void ToUp(ref Int32 x, ref Int32 y)
        {
            --x;
        }

        /// <summary>
        /// Set the parameter coordinates to a new position.
        /// </summary>
        /// <param name="x">The first coordinate of the original position.</param>
        /// <param name="y">The second coordinate of the original position.</param>
        private void ToRightUp(ref Int32 x, ref Int32 y)
        {
            --x;
            ++y;
        }

        /// <summary>
        /// Set the parameter coordinates to a new position.
        /// </summary>
        /// <param name="x">The first coordinate of the original position.</param>
        /// <param name="y">The second coordinate of the original position.</param>
        private void ToRight(ref Int32 x, ref Int32 y)
        {
            ++y;
        }

        /// <summary>
        /// Set the parameter coordinates to a new position.
        /// </summary>
        /// <param name="x">The first coordinate of the original position.</param>
        /// <param name="y">The second coordinate of the original position.</param>
        private void ToRightDown(ref Int32 x, ref Int32 y)
        {
            ++x;
            ++y;
        }

        /// <summary>
        /// Set the parameter coordinates to a new position.
        /// </summary>
        /// <param name="x">The first coordinate of the original position.</param>
        /// <param name="y">The second coordinate of the original position.</param>
        private void ToDown(ref Int32 x, ref Int32 y)
        {
            ++x;
        }

        /// <summary>
        /// Set the parameter coordinates to a new position.
        /// </summary>
        /// <param name="x">The first coordinate of the original position.</param>
        /// <param name="y">The second coordinate of the original position.</param>
        private void ToLeftDown(ref Int32 x, ref Int32 y)
        {
            ++x;
            --y;
        }

        /// <summary>
        /// Set the parameter coordinates to a new position.
        /// </summary>
        /// <param name="x">The first coordinate of the original position.</param>
        /// <param name="y">The second coordinate of the original position.</param>
        private void ToLeft(ref Int32 x, ref Int32 y)
        {
            --y;
        }

        /// <summary>
        /// Set the parameter coordinates to a new position.
        /// </summary>
        /// <param name="x">The first coordinate of the original position.</param>
        /// <param name="y">The second coordinate of the original position.</param>
        private void ToLeftUp(ref Int32 x, ref Int32 y)
        {
            --x;
            --y;
        }

        #endregion

        #endregion

        #region Private event methods

        /// <summary>
        /// Invoke the 'SetGameEnded' event handler if it is set.
        /// </summary>
        /// <param name="arg">The event hadler argumentum.</param>
        private void OnSetGameEnded(ReversiSetGameEndedEventArgs arg)
        {
            if (SetGameEnded != null)
            {
                SetGameEnded(this, arg);
            }
        }

        /// <summary>
        /// Invoke the 'UpdatePlayerTime' event handler if it is set.
        /// </summary>
        /// <param name="arg">The event hadler argumentum.</param>
        private void OnUpdatePlayerTime(ReversiUpdatePlayerTimeEventArgs arg)
        {
            if (UpdatePlayerTime != null)
            {
                UpdatePlayerTime(this, arg);
            }
        }

        /// <summary>
        /// Invoke the 'UpdateTable' event handler if it is set.
        /// </summary>
        /// <param name="arg">The event hadler argumentum.</param>
        private void OnUpdateTable(ReversiUpdateTableEventArgs arg)
        {
            if (UpdateTable != null)
            {
                UpdateTable(this, arg);
            }
        }

        /// <summary>
        /// The timer call this at every tick.
        /// </summary>
        /// <param name="sender">Provides data for the Timer.Elapsed event</param>
        /// <param name="e">The caller object. This class: ReversiGameModel.</param>
        private void Timer_Elapsed(Object sender, ElapsedEventArgs e)
        {
            if(_isPlayer1TurnOn)
            {
                ++(_data.Player1Time);
                OnUpdatePlayerTime(new ReversiUpdatePlayerTimeEventArgs(_isPlayer1TurnOn, _data.Player1Time));
            }
            else
            {
                ++(_data.Player2Time);
                OnUpdatePlayerTime(new ReversiUpdatePlayerTimeEventArgs(_isPlayer1TurnOn, _data.Player2Time));
            }
        }

        #endregion


        private void Help()
        {
            String str = "";
            for (Int32 r = 0; r < _data.TableSize; ++r)
            {
                for (Int32 h = 0; h < _data.TableSize; ++h)
                {
                    if (_table[r, h] != -1)
                        str += " " + _table[r, h].ToString() + " ";
                    else
                        str += _table[r, h].ToString() + " ";
                }
                str += "\n";
            }
            MessageBox.Show(str, "HELP");
        }

        private void Help2()
        {
            Int32[,] alma = new Int32[_data.TableSize, _data.TableSize];
            for (Int32 r = 0; r < _data.TableSize; ++r)
            {
                for (Int32 h = 0; h < _data.TableSize; ++h)
                {
                    alma[r, h] = 0;
                }
            }

            for (Int32 r = 0; r < _possiblePutDownsSize; r += 3)
            {
                alma[_possiblePutDowns[r], _possiblePutDowns[r + 1]] = _possiblePutDowns[r + 2];
            }
            String str = "";
            for (Int32 r = 0; r < _data.TableSize; ++r)
            {
                for (Int32 h = 0; h < _data.TableSize; ++h)
                {
                    str += " " + alma[r, h].ToString() + " ";
                }
                str += "\n";
            }
            MessageBox.Show(str, "HELP2");
        }
    }
}
