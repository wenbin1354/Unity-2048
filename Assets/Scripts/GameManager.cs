using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    [SerializeField] private int _width = 4;
    [SerializeField] private int _height = 4;
    [SerializeField] private Node _nodePrefab;
    [SerializeField] private SpriteRenderer _boardPrefab;
    [SerializeField] private Block _blockPrefab;
    [SerializeField] private List<BlockType> _blockTypes;
    [SerializeField] private float _travelTime = 0.2f;
    [SerializeField] private int _winValue = 2048;

    [SerializeField] private GameObject _winPanel, _losePanel;

    private List<Node> _nodes;
    private List<Block> _blocks;
    private GameState _state;
    private int _round;

    private BlockType GetBlockTypeByValue(int value) => _blockTypes.First(type => type.Value == value);


    void Start()
    {
        ChangeState(GameState.GenerateLevel);
    }


    private void ChangeState(GameState newState)
    {
        _state = newState;

        switch (newState)
        {
            case GameState.GenerateLevel:
                GenerateGrid();
                break;
            case GameState.SpawningBlocks:
                SpawnBlocks(_round++ == 0 ? 2 : 1);
                break;
            case GameState.WaitingInput:
                break;
            case GameState.Moving:
                break;
            case GameState.Win:
                _winPanel.SetActive(true);
                break;
            case GameState.Lose:
                _losePanel.SetActive(true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }


    void Update()
    {
        if (_state != GameState.WaitingInput) return;

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveBlocks(Vector2.up);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveBlocks(Vector2.down);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            MoveBlocks(Vector2.left);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveBlocks(Vector2.right);
        }
    }



    // generate the 4 by 4 grid of 2048
    void GenerateGrid()
    {
        _round = 0;
        // intializing the nodes and blocks list to store them
        _nodes = new List<Node>();
        _blocks = new List<Block>();

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                // Generate a tile at position x, y
                var node = Instantiate(_nodePrefab, new Vector2(x, y), Quaternion.identity);

                // add the node to the list
                _nodes.Add(node);
            }
        }

        var center = new Vector2((float)_width / 2 - 0.5f, (float)_height / 2 - 0.5f);

        var board = Instantiate(_boardPrefab, center, Quaternion.identity);

        board.size = new Vector2(_width, _height);

        Camera.main.transform.position = new Vector3(center.x, center.y, -10);

        ChangeState(GameState.SpawningBlocks);
    }


    // spawning the blocks
    void SpawnBlocks(int amount)
    {
        // get the free nodes
        var freeNodes = _nodes.Where(node => node.OccupiedBlock == null).OrderBy(node => Random.value).ToList();


        // take a certain amount from free node, loop over and spawn block randomly
        foreach (var node in freeNodes.Take(amount))
        {
            SpawnBlock(node, Random.value > 0.9f ? 4 : 2);
        }


        if (freeNodes.Count() == 0)
        {
            // lost the game
            ChangeState(GameState.Lose);
            return;
        }

        ChangeState(_blocks.Any(block => block.Value == _winValue) ? GameState.Win : GameState.WaitingInput);
    }

    // spawn a block at a certain node
    void SpawnBlock(Node node, int value)
    {
        var block = Instantiate(_blockPrefab, node.Pos, Quaternion.identity);
        block.Init(GetBlockTypeByValue(value));
        block.SetBlock(node);
        _blocks.Add(block);
    }


    // moving the blocks toward the direction
    void MoveBlocks(Vector2 direction)
    {
        ChangeState(GameState.Moving);

        var orderedBlocks = _blocks.OrderBy(block => block.Pos.x).ThenBy(block => block.Pos.y).ToList();

        if (direction == Vector2.right || direction == Vector2.up)
        {
            orderedBlocks.Reverse();
        }

        foreach (var block in orderedBlocks)
        {

            var next = block.Node;

            do
            {
                block.SetBlock(next);

                var possibleNode = GetNodeAt(next.Pos + direction);

                // we know that a node is present
                if (possibleNode != null)
                {
                    // if occupied by a block of the same value we can merge
                    if (possibleNode.OccupiedBlock != null && possibleNode.OccupiedBlock.CanMerge(block.Value))
                    {
                        // so that this is in process of merging
                        block.MergeBlock(possibleNode.OccupiedBlock);

                    }

                    // if null we can take the space and move it
                    else if (possibleNode.OccupiedBlock == null)
                    {
                        next = possibleNode;
                    }
                }

            } while (next != block.Node);

            block.transform.DOMove(block.Node.Pos, _travelTime);

        }

        var sequence = DOTween.Sequence();

        foreach(var block in orderedBlocks)
        {
            var movePoint = block.MergingBlock != null ? block.MergingBlock.Node.Pos : block.Node.Pos;

            sequence.Insert(0, block.transform.DOMove(movePoint, _travelTime));
        }

        sequence.OnComplete(() =>
        {
            foreach (var block in orderedBlocks.Where(block => block.MergingBlock != null))
            {
                MergeBlock(block.MergingBlock, block);
            }

            ChangeState(GameState.SpawningBlocks);

        });

    }

    // merge the blocks
    void MergeBlock(Block baseBlock, Block mergingBlock)
    {
        SpawnBlock(baseBlock.Node, baseBlock.Value + mergingBlock.Value);

        RemoveBlock(baseBlock);
        RemoveBlock(mergingBlock);
    }

    void RemoveBlock(Block block)
    {
        _blocks.Remove(block);
        Destroy(block.gameObject);
    }


    // get the node at a certain position
    Node GetNodeAt(Vector2 pos)
    {
        return _nodes.FirstOrDefault(n => n.Pos == pos);
    }


}

[Serializable]
public struct BlockType
{
    public int Value;
    public Color Color;
}

public enum GameState
{
    GenerateLevel,
    SpawningBlocks,
    WaitingInput,
    Moving,
    Win,
    Lose
}