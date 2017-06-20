using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexChunk : MonoBehaviour {

    //set a const for sqrt(3) to save some processing time 
    const float SQRT3 = 1.732050807568877f;
    //variables to keep track of the left most and right most vertex indices in the chunk
    //these will be used for scrolling in the HexMap class later.
    public int left_start_vertex_index;
    public int right_start_vertex_index;

    public void BuildChunk(float start_x, float start_z, int chunk_width, int chunk_depth, float scale) {
        //create a mesh to hold all the chunk data
        Mesh chunk_mesh = new Mesh();
        //next lets calculate the number of vertices needed for the mesh(chunk)
        //since 7 vertices per hex and width times depth number of hexes 
        //number of vertices is simply the multiple of these values
        int num_vertices = chunk_width * chunk_depth * 7;
        //create an array to hold all the vertices for the chunk
        //>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>IMPORTANT!!!<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<< 
        //you need to assign the mesh vertices property from the chunk_mesh
        //to the local variable first before you begin to manipulate it...then you can assign it back at
        //the end. I am not 100% sure why but if you don't the mesh doesn't work properly. 
        Vector3[] vertices = chunk_mesh.vertices;
        vertices = new Vector3[num_vertices];
        //now we need to build an array of color information about each vertex so our
        //shader can properly color the map
        Color32[] colors = chunk_mesh.colors32;
        colors = new Color32[num_vertices];
        //next lets calculate the number of triangle indices for the mesh(chunk)
        //since there are 3 indices per triangle and 6 triangles per hex and the 
        //width times the depth number of hexes the number of triangle indices
        //is simply the multiple of these values (I precomuted the 3x6 value)
        int num_triangle_indices = chunk_width * chunk_depth * 18;
        //create an array to hold all the triangle indices needed for the mesh (chunk)
        int[] triangles = new int[num_triangle_indices];
        //create a counter to keep track of which vertex index we are at in the mesh
        int mesh_vertex_index = 0;
        //create a counter to keep track of which triangle index we are at in the mesh
        int mesh_triangle_index = 0;
        //set the location for the depth
        float depthpos = start_z;
        //BTW I know a number of the static values below (SQRT3 * scale / 2f for instance)
        //could be optimized a bit by simply precalculating them once and using a variable
        //I haven't done that to make it a little easier to follow whats going on
        //in a finished product I would look to a bunch of places in this code where
        //processing cycles and memory could be optimized.
        //now we need to loop through all the hexes that make up a chunk
        //setting the vertices for each one starting with the depth(z axis)
        for(int cz = 0; cz < chunk_depth; cz++) {
            //set the initial position of our hex in the x axis
            float widthpos = start_x;
            //if we are on an odd row we need to offset the starting position by the radius 
            //plus an offset (which is half the size of the radius) again remember we are using
            //the HexScale(passed in as scale) as the radius in world units here I have precomputed the radius plus
            //the extra half radius by muliplying by 1.5
            //now we loop through all the hexes in the width (x axis) setting the vertices
            for(int cx = 0; cx < chunk_width; cx++) {
                float depth_offset = 0;
                if(cx % 2 == 1) {
                    depth_offset = SQRT3 * scale / 2.0f;
                }
                //Debug.Log("     Building chunk hex (cx,cz) =(" + cx + "," + cz + ")");
                //call a method to populate the vertices array with the vertices for this hex
                //starting with the variable we are using to keep track of where in the mesh
                //we are at.  Use a vector containing the position of the hexes center as the 
                //point to work with to calc the vertices
                BuildHexVertices(mesh_vertex_index, vertices, new Vector3(widthpos, 0f, depthpos + depth_offset), scale);

                //get a random color from the ones set in editor
                //int coloridx = Random.Range(0, HexColors.Length - 2);
                Color32 hexcolor = new Color32(0, 0, 128, 255); //HexColors[coloridx];
                if(cx==0) {
                    hexcolor = new Color32(128, 0, 0, 255); //HexColors[HexColors.Length - 1];
                    if(cz == 0) {
                        //set the start vertex index for the left side;
                        left_start_vertex_index = mesh_vertex_index;
                    }
                }
                if(cx == chunk_width - 1) {
                    hexcolor = new Color32(0, 128, 0, 255); //HexColors[HexColors.Length - 2];
                    if(cz == 0) {
                        right_start_vertex_index = mesh_vertex_index;
                    }
                }
                BuildHexColors(mesh_vertex_index, colors, hexcolor);
                //call a method to populate the triangle indices array with the indices of the 
                //triangles contained in this hex starting with the variable we are using to keep track
                //of where in the mesh we are at.  use the current vertex index in the mesh as a starting
                //point to calculate the triangle indices
                BuildHexTriangles(mesh_triangle_index, triangles, mesh_vertex_index);
                //increment the vertex index to the next hex 
                mesh_vertex_index += 7;
                //increment the triangle index to the next hex
                mesh_triangle_index += 18;
                //increment the location of the x-axis for the next hexagon which will be one full radius 
                //away plus one halfradius(scale)  Here I have precomputed that to be 1.5 times
                //the radius
                //      _____     *
                //     /     \         /
                //    /       \   R   /
                //   <    *  R >_____<  
                //    \       /       \
                //     \ ___ /         \
                //        |<-1.5R->|               
                widthpos += scale * 1.5f;
            }
            //increment the location of the  z-axis for the next hexagaon which will be
            //the full height(H) of a hexagon 
            //        _____  
            //       /     \          
            //  --  /   .   \       
            //   |  \       /      
            //   H   \ ___ /       
            //   |   /     \        
            //  --  /   .   \  
            //      \       /      
            //       \ ___ /       
            // H = sqrt(3) * radius (scale)  
            depthpos += SQRT3 * scale;
        }
        //assign the vertices to the mesh
        chunk_mesh.vertices = vertices;
        //assign the colors of the vertices to the mesh
        chunk_mesh.colors32 = colors;
        //assign the triangle indices to the mesh
        chunk_mesh.triangles = triangles;
        //force a recalculation of the mesh bounds and normals
        chunk_mesh.RecalculateBounds();
        chunk_mesh.RecalculateNormals();
        //set the mesh property on the chunk prefab to the mesh we
        //have just built
        gameObject.GetComponent<MeshFilter>().mesh = chunk_mesh;

    }

    //this builds out the colors of each vertex associated with the hex...then our shader applies the colors
    //to the vertices to color our hexes
    private void BuildHexColors(int start_index, Color32[] colors, Color32 hexcolor) {
        //assign that color to all the vertices
        colors[start_index++] = hexcolor;
        colors[start_index++] = hexcolor;
        colors[start_index++] = hexcolor;
        colors[start_index++] = hexcolor;
        colors[start_index++] = hexcolor;
        colors[start_index++] = hexcolor;
        colors[start_index++] = hexcolor;
    }

    //this method builds out the triangle indices for a hex given the starting triangle index,
    //the triangle indices array and the starting vertex index
    private void BuildHexTriangles(int triangle_idx, int[] triangles, int vertex_idx) {
        //here again is the vertices...now we want to build out triangles
        //             2_____3 
        //             /\   /\
        //            /  \ /  \
        //          1<---0+---->4
        //            \  / \  /
        //             \/___\/
        //             6     5
        //each tri has 3 indices and there are 6 tris
        //so we need 18 indices to hold entire hex
        //note the orientation here..we are assigning the 
        //indices in a counter-clockwise order...this will
        //insure the normals are facing up...if you do this
        //in the clockwise order you won't see anything render
        //as the faces are one-sided and are facing away from 
        //you.
        triangles[triangle_idx++] = vertex_idx + 0;
        triangles[triangle_idx++] = vertex_idx + 2;
        triangles[triangle_idx++] = vertex_idx + 1;
        triangles[triangle_idx++] = vertex_idx + 0;
        triangles[triangle_idx++] = vertex_idx + 3;
        triangles[triangle_idx++] = vertex_idx + 2;
        triangles[triangle_idx++] = vertex_idx + 0;
        triangles[triangle_idx++] = vertex_idx + 4;
        triangles[triangle_idx++] = vertex_idx + 3;
        triangles[triangle_idx++] = vertex_idx + 0;
        triangles[triangle_idx++] = vertex_idx + 5;
        triangles[triangle_idx++] = vertex_idx + 4;
        triangles[triangle_idx++] = vertex_idx + 0;
        triangles[triangle_idx++] = vertex_idx + 6;
        triangles[triangle_idx++] = vertex_idx + 5;
        triangles[triangle_idx++] = vertex_idx + 0;
        triangles[triangle_idx++] = vertex_idx + 1;
        triangles[triangle_idx++] = vertex_idx + 6;
    }

    //this method builds out the vertices for a hex given the starting vertex index, the 
    //vertices array and the center point for the new hexagon.
    private void BuildHexVertices(int start_index, Vector3[] vertices, Vector3 hex_center, float scale) {
        //here are the vertices we need to build
        //             2_____3 
        //             /\   /\
        //            /  \ /  \
        //          1<---0+---->4
        //            \  / \  /
        //             \/___\/
        //             6     5
        //using that and the diagram we used before with some
        //important measurements
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
        // we can calcuate all the vertices
        //first lets get some of the basic measurements calculated
        //remember we are using the HexScale as the basic measurement
        //of the radius in world units
        float radius = scale;
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
