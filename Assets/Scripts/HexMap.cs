using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexMap : MonoBehaviour {
    //the width of the map in hexes
    public int Width;
    //the depth of the map in hexes
    public int Depth;
    //the size of each hex in unity units
    public float HexScale;

    //a prefab containing the chunk 
    public GameObject HexChunk;
    //create an array of possible colors to use
    //to color the hexes
    public Color32[] HexColors;

    //there is a limitation in Unity that any individual mesh cannot contain more than 65535 verts
    //I set up each hexagon to have 7 vertices (there are other ways to triangulate a hexagon and some of those 
    //require one less vertex but for texturing and (my)ease of understanding I choose this triangulation)
    //also note here the I am using an orientation with flat versus pointy tops.  An endless debate rages
    //over the best orientation...I picked one.  It doesn't matter here but it will once I start setting 
    //vertices.
    //             2_____3 
    //             /\   /\
    //            /  \ /  \
    //          1<---0+---->4
    //            \  / \  /
    //             \/___\/
    //             6     5
    //if we have 7 verts per hex and a max of 65535 verts per mesh then the max hexes per mesh or what we are
    //going to call a "chunk" is 655535 / 7 = 9362.142857 (Again I am duplicating vertices at each corner...
    //it's possible with some clever math to not have to duplicate the vertices...
    //however this complicates things when you go to texture the hex mesh and makes the scrolling of the
    //map more complex as well.  If I find I have performance issues or I get ambitious I may go back 
    //and implement it so there are no duplicate vertices in the entire chunk)
    //so taking 9362.142857 we can use as a chunk size any width * height which is less than 9362 
    //since we are looking to build large tile maps I am simply going to create square chunks by taking the 
    //square root of 9362.142857 to get 96.758 or simply 96...if you were looking to maps that were much wider than
    //high it might make sense to use rectangular chunks say 128 wide by 72 high...if you are making maps smaller than
    //the chunk sizes you don't need to do this chunking at all.
    //const int CHUNK_WIDTH = 96;
    //const int CHUNK_DEPTH = 96;
    const int CHUNK_WIDTH = 16;
    const int CHUNK_DEPTH = 16;
    //set a const for sqrt(3) to save some processing time 
    const float SQRT3 = 1.732050807568877f;
    //a variable to keep track of how much we have panned so we can determine
    //when we need to move hexes(vertices) so we can scroll endlessly
    private float total_pan_distance = 0;
    //a 2D array of chunks which we will need for managing scrolling
    private HexChunk[,] hexchunks;
    //variables holding the size of the map in chunks
    private int chunks_wide;
    private int chunks_deep;
    //variable to hold the distance the map must translate before triggering
    //a movement of the hex vertices
    private float hex_trigger;
    //variables holding the indices of the leftmost and right most chunk
    private int left_chunk_idx;
    private int right_chunk_idx;
    //variable holding the number of vertices in each hex column
    private int vertices_per_row;
    //variable holding the width of the map in unity space
    private float map_width;

    // Set up hexmap
    void Start() {
        //generate the map
        GenerateHexMap();
        //initialize the left and right chunk indices
        left_chunk_idx = 0;
        right_chunk_idx = chunks_wide - 1;
        //do a quick calculation on the number of vertices contained in each row 
        //this is used when scrolling the map
        vertices_per_row = CHUNK_WIDTH * 7;
        //do a precalculation of the width of the map ..this is used when scrolling
        map_width = chunks_wide * CHUNK_WIDTH * HexScale * 1.5f;
    }

    //this method gets called by the mousemanger each time we pan the map so 
    //we can update the total panning distance and then move hexes as needed 
    public void UpdatePanDistance(Vector3 pan_delta) {
        //update the distance
        total_pan_distance += pan_delta.x;
        //if the total distance moved is more than one hex
        //in either direction move the vertices (there may be
        //a more efficient way to do this that would not move hexes unless
        //they were in view but this works and the performance is adequate)
        if(total_pan_distance > hex_trigger) {
            //Debug.Log("move hexes left! " + total_pan_distance);
            MoveHexesLeft();
            //once we have moved hexes reset the counter so we will not keep
            //moving.  NOTE: instead of resetting to zero reset it by subtracting the distance 
            //we are checking against this keeps us from having a small error creep that results
            //from the total pan distance being slightly more than the hex width when we do the move
            total_pan_distance -= hex_trigger;
        } else if(total_pan_distance < -hex_trigger) {
            //Debug.Log("move hexes right! " + total_pan_distance);
            MoveHexesRight();
            //once we have moved hexes reset the counter so we will not keep
            //moving.  NOTE: instead of resetting to zero reset it by subtracting the distance 
            //we are checking against this keeps us from having a small error creep that results
            //from the total pan distance being slightly more than the hex width when we do the move
            total_pan_distance += hex_trigger;
        }
    }

    //this is the method which is going to generate the map by breaking it into chunks(meshes) and building out 
    //appropriate hexes with vertices for each chunk
    private void GenerateHexMap() {
        //first take Map dimensions and figure out the number of chunks required
        //to be built  
        //*********************** NOTE ******************************************
        //right now I don't want to deal with partial chunks so I am going to 
        //limit my map width and height to multiples of the chunk size
        chunks_wide = Mathf.FloorToInt(Width / CHUNK_WIDTH);
        chunks_deep = Mathf.FloorToInt(Depth / CHUNK_DEPTH);
        //Debug.Log("chunks(wide, deep)=(" + chunks_wide + "," + chunks_deep + ")");
        //TODO: deal with partial chunks so we always end up with enough chunks to 
        //contain all the hexes 
        //float chunk_width = Width / CHUNK_WIDTH;
        //float chunk_depth = Depth / CHUNK_DEPTH);

        //so more math...we know the size in chunks now but in order to set the vertices 
        //we have to take into account that we want the camera to stay stationary above 
        //the map at some fixed height  ie camera position = (0, height, 0)  so we have to 
        //set the vertices of the hexes so that the camera position is at the center
        //(later we will "scroll" the map by moving the hex vertices around and flipping 
        //them as needed from right to left {or left to right depending on scroll direction}
        //as well as top to bottom {or again bottom to top})
        //for now since we know the width of each chunk and the number of chunks wide the map is
        //we can calculate the leftmost center vertex of the map....first lets look at a hex
        //           |-----S----|-O-|  
        //    _          _______    
        //    |         /\  R  /\   
        //    |        /  \   /  \  
        //    |       /  R \ /  R \ 
        //   H|      <------+------> -+-
        //    |       \    / \    /   |
        //    |        \  /   \  /    C
        //    |_        \/_____\/    _|_
        //           |------W------|   
        //this is where using the flat topped orientation comes into play.  the width(W) a hex is
        //twice it's radius(R) but because each row is offset by we need to adjust the width by half
        //again the total, first get a total width.. this is too small because half the hexes are offset(O)
        //in our case we are just using the HexScale to be the radius in world units so half the hexes
        //will contribute their full width
        float world_chunk_width = CHUNK_WIDTH / 2f * HexScale * 2f;
        //now add the amount of the offset(O) that half the hexes contribute
        //so half the chunks only contribute half the width or one radius
        //BTW - I know this can be reduced but just for ease of readability doing it in two steps
        world_chunk_width += CHUNK_WIDTH / 2f * HexScale;
        //next take the width of each chunk and since we want it centered on the map multiply it by 
        //half the number of width chunks and make it negative
        float map_left_edge = -world_chunk_width * (chunks_wide / 2f);
        //finally get right edge which should just be neg of left edge since we are centered
        float map_right_edge = -map_left_edge;
        //set the value for the trigger which causes the map to move hexes
        //when scrolling
        hex_trigger = Mathf.Abs((map_left_edge * 2f) / (CHUNK_WIDTH * chunks_wide));
        //next we need to do the same for height(H)...but there is a twist the height(H) isn't a simple 
        //multiple of the radius(R) like the width(W) is after some fancy geometry it turns out that
        //the height(H) is equal to the square root of 3 times the radius (R)
        //so the total is the number of hexes deep the chunk is times the height(H) of each hex and again
        //factor in the scaling
        float world_chunk_depth = CHUNK_DEPTH * SQRT3 * HexScale;
        //next take the depth of each chunk and since we want it centered on the map multiply it by
        //half the number of chunks deep and make it negative
        float map_top_edge = -world_chunk_depth * (chunks_deep / 2f);
        //finally get bottom edge which should just be neg of top edge since we are centered
        float map_bottom_edge = -map_top_edge;
        //Debug.Log("chunk depth=" + world_chunk_depth + "   map top edge=" + map_top_edge);

        hexchunks = new HexChunk[chunks_wide, chunks_deep];
        //now that we have the starting point we can begin building out our chunks
        //loop though the chunk zs starting with top edge and going to bottom
        for(int z = 0; z < chunks_deep; z++) {
            //next loop through the chunk xs starting from left edge and moving to right
            for(int x = 0; x < chunks_wide; x++) {
                //Debug.Log("Building chunk (x,z) =(" + x + "," + z + ")");
                //first lets instaniate a prefab chunk of hexes
                GameObject chunk = Instantiate(HexChunk, gameObject.transform);
                //give it a name to distinguish it
                chunk.name = "chunk(" + x + "," + z + ")";
                //get reference to script attached to chunk
                hexchunks[x, z] = chunk.GetComponent<HexChunk>();
                //calculate world position of chunk
                float chunk_position_x = map_left_edge + x * world_chunk_width;
                float chunk_position_z = map_top_edge + z * world_chunk_depth;
                //build out the chunk
                hexchunks[x, z].BuildChunk(chunk_position_x, chunk_position_z, CHUNK_WIDTH, CHUNK_DEPTH, HexScale);
            }
        }
    }

    //A really clever programmer could probably combine these two functions (MoveHexesRight and MoveHexesLeft) into one
    //the logic is nearly identical...just some sign differences and a few test boundaries (both of which could probably be 
    //parameterized)  I haven't done it because it's easier to read this way and there would be no significant performance boost

    //this method picks up the vertices from the left most 
    //chunk and moves them to the right side of the map.
    public void MoveHexesRight() {
        //for each row of chunks in the map
        //we need to move verts from left side of map to right side
        for(int z = 0; z < chunks_deep; z++) {
            //Debug.Log("left start, right start (" + hexchunks[left_chunk_idx, z].left_start_vertex_index + ", " 
            //+ hexchunks[left_chunk_idx, z].right_start_vertex_index + ")");
            //to move the vertices first we need to get a local copy from the chunk game object
            Vector3[] vertices = hexchunks[left_chunk_idx, z].gameObject.GetComponent<MeshFilter>().mesh.vertices;
            //all the hexes on the left side of this chunk move vertices(hexes)to be on the right side
            for(int ctr = 0; ctr < CHUNK_DEPTH; ctr++) {
                //get the index of the first vertex for the hex we are about to move
                int hex_vertex_index = hexchunks[left_chunk_idx, z].left_start_vertex_index + (ctr * vertices_per_row);
                //the way we have arranged the hex vertices the first one is always the center.  so get the 
                //existing hex center vertex
                Vector3 old_hex_center = vertices[hex_vertex_index];
                //calcuate where the new center of the vertex would be..the y and z values stay the same but the x value
                //moves all the way to the opposite side of the map.
                //I have precalculated the offset to move to the opposite side of the map
                // map_width = (chunks_wide * CHUNK_WIDTH * HexScale * 1.5f)
                Vector3 new_hex_center = new Vector3(old_hex_center.x + map_width, old_hex_center.y, old_hex_center.z);
                //set the remaining vertices for this hex
                SetHexVertices(hex_vertex_index, vertices, new_hex_center);
            }
            //push the moved vertices back into the game object
            hexchunks[left_chunk_idx, z].gameObject.GetComponent<MeshFilter>().mesh.vertices = vertices;
            hexchunks[left_chunk_idx, z].gameObject.GetComponent<MeshFilter>().mesh.RecalculateBounds();
            //commenting this out...maybe we need to do this or maybe not...not sure yet
            //hexchunks[left_chunk_idx, z].gameObject.GetComponent<MeshFilter>().mesh.RecalculateNormals();

            //set right vertex index to the old left vertex index since its now on the right side
            hexchunks[left_chunk_idx, z].right_start_vertex_index = hexchunks[left_chunk_idx, z].left_start_vertex_index;
            //since we are moving this chunk to right side that makes this the right side chunk now
            //left chunk won't change unless we have run over the width of the chunk which is tested for next
            right_chunk_idx = left_chunk_idx;
            //move left vertex index over one hex
            hexchunks[left_chunk_idx, z].left_start_vertex_index += 7;
            //if the start index has scrolled all the way around we need to 
            //reset it so we don't run over the end of the vertices array
            if(hexchunks[left_chunk_idx, z].left_start_vertex_index >= vertices_per_row) {
                hexchunks[left_chunk_idx, z].left_start_vertex_index = 0;
                //if we are on the last row check to see if we need to move our
                //left chunk index to point at a new chunk
                if(z == chunks_deep - 1) {
                    //if we are more than one chunk wide we need to move to next chunk(in the x-axis) if we are at the
                    //last chunk wide we need to move back to beginning
                    left_chunk_idx++;
                    if(left_chunk_idx >= chunks_wide) {
                        left_chunk_idx = 0;
                    }
                }
            }
        }
    }

    //this method picks up the vertices from the right most
    //chunk and moves them to the left side of the map.
    public void MoveHexesLeft() {
        //for each row of chunks in the map
        //we need to move verts from right side of map to left side
        for(int z = 0; z < chunks_deep; z++) {
            //Debug.Log("left start, right start (" + hexchunks[right_chunk_idx, z].left_start_vertex_index + ", "
            //+ hexchunks[right_chunk_idx, z].right_start_vertex_index + ")");
            //to move the vertices first we need to get a local copy from the chunk game object
            Vector3[] vertices = hexchunks[right_chunk_idx, z].gameObject.GetComponent<MeshFilter>().mesh.vertices;
            //all the hexes on the right side of this chunk move vertices(hexes)to be on the left side
            for(int ctr = 0; ctr < CHUNK_DEPTH; ctr++) {
                //get the index of the first vertex for the hex we are about to move
                int hex_vertex_index = hexchunks[right_chunk_idx, z].right_start_vertex_index + (ctr * vertices_per_row);
                //the way we have arranged the hex vertices the first one is always the center.  so get the 
                //existing hex center vertex
                Vector3 old_hex_center = vertices[hex_vertex_index];
                //calcuate where the new center of the vertex would be..the y and z values stay the same but the x value
                //moves all the way to the opposite side of the map.
                //I have precalculated the offset to move to the opposite side of the map
                // map_width = (chunks_wide * CHUNK_WIDTH * HexScale * 1.5f)
                Vector3 new_hex_center = new Vector3(old_hex_center.x - map_width, old_hex_center.y, old_hex_center.z);
                //set the remaining vertices for this hex
                SetHexVertices(hex_vertex_index, vertices, new_hex_center);
            }
            //push the moved vertices back into the game object
            hexchunks[right_chunk_idx, z].gameObject.GetComponent<MeshFilter>().mesh.vertices = vertices;
            hexchunks[right_chunk_idx, z].gameObject.GetComponent<MeshFilter>().mesh.RecalculateBounds();
            //commenting this out...maybe we need to do this or maybe not...not sure yet
            //hexchunks[right_chunk_idx, z].gameObject.GetComponent<MeshFilter>().mesh.RecalculateNormals();
            //set left vertex index to the old right vertex index since its now on the left side
            hexchunks[right_chunk_idx, z].left_start_vertex_index = hexchunks[right_chunk_idx, z].right_start_vertex_index;
            //since we are moving this chunk to left side that makes this the left side chunk now
            //right chunk won't change unless we have run over the width of the chunk which is tested for next
            left_chunk_idx = right_chunk_idx;
            //move right vertex over one hex
            hexchunks[right_chunk_idx, z].right_start_vertex_index -= 7;
            //if the start index has scrolled all the way around we need to 
            //reset it so we don't run over the end of the vertices array
            if(hexchunks[right_chunk_idx, z].right_start_vertex_index < 0) {
                hexchunks[right_chunk_idx, z].right_start_vertex_index = vertices_per_row - 7;
                //if we are on the last row check to see if we need to move our
                //right chunk index to point at a new chunk
                if(z == chunks_deep - 1) {
                    //if we are more than one chunk wide we need to move to previous chunk(in the x-axis) if we are at the
                    //first chunk we need to move back to chunk at the end
                    right_chunk_idx--;
                    if(right_chunk_idx < 0) {
                        right_chunk_idx = chunks_wide - 1;
                    }
                }
            }
        }
    }

    //just uses some hex math geometry to set vertices based on center vertex
    public void SetHexVertices(int start_index, Vector3[] vertices, Vector3 hex_center) {
        float radius = HexScale;
        //float width = 2.0f * radius;
        //float side = 1.5f * radius;
        float height = SQRT3 * radius;
        float center = height / 2.0f;
        float offset = 0.5f * radius;
        //first vertex will just be the center one passed in
        vertices[start_index++] = hex_center;
        //now use the measurements we calculated above to get the rest of the vertices
        vertices[start_index++] = new Vector3(hex_center.x - radius, 0f, hex_center.z);
        vertices[start_index++] = new Vector3(hex_center.x - offset, 0f, hex_center.z - center);
        vertices[start_index++] = new Vector3(hex_center.x + offset, 0f, hex_center.z - center);
        vertices[start_index++] = new Vector3(hex_center.x + radius, 0f, hex_center.z);
        vertices[start_index++] = new Vector3(hex_center.x + offset, 0f, hex_center.z + center);
        vertices[start_index++] = new Vector3(hex_center.x - offset, 0f, hex_center.z + center);
    }

}
