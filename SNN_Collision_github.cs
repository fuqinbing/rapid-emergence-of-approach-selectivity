/*
 * Filename: SNN_Collision_github.cs
 * Author: Dr. Qinbing Fu
 * Email: qifu@gzhu.edu.cn
 */

using System;


namespace LGMD
{
    /// <summary>
    /// This class is in support of constructing the model within paper
    /// "Rapid emergence of approach selectivity in a biologically structured neural model with spike timing-dependent plasticity"
    /// </summary>
    internal class SNN_Collision_github : sLGMD
    {
        #region FIELD
        /// <summary>
        /// excitorary post-synaptic current of ON channels
        /// </summary>
        protected double[,] epsc_ONs { get; private set; }
        /// <summary>
        /// inhibitory post-synaptic current of ON channels
        /// </summary>
        protected double[,] ipsc_ONs { get; private set; }
        /// <summary>
        /// excitorary post-synaptic current of OFF channels
        /// </summary>
        protected double[,] epsc_OFFs { get; private set; }
        /// <summary>
        /// inhibitory post-synaptic current of OFF channels
        /// </summary>
        protected double[,] ipsc_OFFs { get; private set; }
        /// <summary>
        /// threshold of activation population of feed-forward inhibition neurons
        /// </summary>
        protected double ffi_population_thre { get; private set; }
        /// <summary>
        /// ON-contrast sensitive spiking neuron
        /// </summary>
        protected byte[] spiON { get; private set; }
        /// <summary>
        /// OFF-contrast sensitive spiking neuron
        /// </summary>
        protected byte[] spiOFF { get; private set; }
        /// <summary>
        /// current of ON-constrast sensitive spiking neuron
        /// </summary>
        protected double[] I_ON { get; private set; }
        /// <summary>
        /// current of OFF-contrast sensitive spiking neuron
        /// </summary>
        protected double[] I_OFF { get; private set; }
        /// <summary>
        /// voltage of ON-contrast sensitive spiking neuron
        /// </summary>
        protected double[] V_ON { get; private set; }
        /// <summary>
        /// voltage of OFF-contrast sensitive spiking neuron
        /// </summary>
        protected double[] V_OFF { get; private set; }
        /// <summary>
        /// post cell firing rate
        /// </summary>
        protected byte postRate { get; private set; }
        /// <summary>
        /// LIF neuron model
        /// </summary>
        public LIF_Neuron_github lif { get; private set; }
        /// <summary>
        /// inhibitory signal coefficient
        /// </summary>
        protected double Cinh { get; private set; }
        /// <summary>
        /// potential collision warning
        /// </summary>
        public bool WARNING { get; private set; }
        #endregion

        #region PROPERTY
        public double[,] EPSC_ON
        {
            get { return epsc_ONs; }
        }
        public double[,] IPSC_ON
        {
            get { return ipsc_ONs; }
        }
        public double[,] EPSC_OFF
        {
            get { return epsc_OFFs; }
        }
        public double[,] IPSC_OFF
        {
            get { return ipsc_OFFs; }
        }
        public byte[] SPI_ON
        {
            get { return spiON; }
        }
        public byte[] SPI_OFF
        {
            get { return spiOFF; }
        }
        #endregion

        #region CONSTRUCTOR
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="width">input stimulus width</param>
        /// <param name="height">input stimulus height</param>
        /// <param name="fps">sampling frequency</param>
        public SNN_Collision_github(int width, int height, int fps) : base(width, height, fps)
        {
            epsc_ONs = new double[ds_height, ds_width];
            epsc_OFFs = new double[ds_height, ds_width];
            ipsc_ONs = new double[ds_height, ds_width];
            ipsc_OFFs = new double[ds_height, ds_width];
            Wffi = 0.5;
            Wlooming = 0.5;
            // attention
            phase_delay = 1;
            ffi_population_thre = 0.2;
            spiON = new byte[8];
            spiOFF = new byte[8];
            I_ON = new double[8];
            I_OFF = new double[8];
            V_ON = new double[8];
            V_OFF = new double[8];
            Vth = 1;
            postRate = 0;
            //
            lif = new LIF_Neuron_github
            {
                iter = 30,
                //step = (1.0 / this.fps) / lif.iter, // seconds
                V0 = -60, // mV
                Vth = -50, // mV
                Vact = 20, // mV
                Vrepo = -70, // mV
                R = 50000,
                tau_m = 0.1
            };
            V_Looming_Neuron = new double[lif.iter];
            for (int i = 0; i < V_Looming_Neuron.Length; i++)
                V_Looming_Neuron[i] = lif.V0;
            Cinh = 1;
            WARNING = false;

            Console.WriteLine("Spiking looming perception & STDP network constructed ......");
        }
        #endregion

        #region METHOD
        /// <summary>
        /// Resize grayscale input signal to a fixed dimension using Bilinear Interpolation
        /// </summary>
        /// <param name="matrix">input signal matrix</param>
        /// <param name="targetRows">target rows</param>
        /// <param name="targetCols">target columns</param>
        /// <returns></returns>
        protected double[,] ResizeMatrix(byte[,,] matrix, int targetRows = 100, int targetCols = 100)
        {
            int originalRows = matrix.GetLength(0);
            int originalCols = matrix.GetLength(1);
            // Create target matrix
            double[,] resizedMatrix = new double[targetRows, targetCols];
            // Compute scales
            double rowScale = (double)originalRows / targetRows;
            double colScale = (double)originalCols / targetCols;
            // Bilinear interpolation for subsampling
            for (int i = 0; i < targetRows; i++)
            {
                for (int j = 0; j < targetCols; j++)
                {
                    // Calculate originate location
                    double originalRow = i * rowScale;
                    double originalCol = j * colScale;
                    // Obtain four neighboring locations
                    int row1 = (int)originalRow;
                    int row2 = Math.Min(row1 + 1, originalRows - 1);
                    int col1 = (int)originalCol;
                    int col2 = Math.Min(col1 + 1, originalCols - 1);
                    // Compute interpolation weights
                    double rowWeight = originalRow - row1;
                    double colWeight = originalCol - col1;
                    // Bilinear interpolation
                    double value = (1 - rowWeight) * (1 - colWeight) * matrix[row1, col1, 0] +
                                   (1 - rowWeight) * colWeight * matrix[row1, col2, 0] +
                                   rowWeight * (1 - colWeight) * matrix[row2, col1, 0] +
                                   rowWeight * colWeight * matrix[row2, col2, 0];
                    // Assign to target matrix
                    resizedMatrix[i, j] = value;
                }
            }
            return resizedMatrix;
        }
        /// <summary>
        /// Gaussian blur
        /// </summary>
        /// <param name="inputMat"></param>
        /// <param name="gauss_width"></param>
        /// <param name="gauss_kernel"></param>
        /// <param name="height"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        protected double[,] GaussBlur(double[,] inputMat, int gauss_width, double[,] gauss_kernel, int height, int width)
        {
            double[,] outputMat = new double[height, width];
            double tmp;
            //kernel radius
            int k_radius = gauss_width / 2;
            int r, c;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int i = -k_radius; i < k_radius + 1; i++)
                    {
                        for (int j = -k_radius; j < k_radius + 1; j++)
                        {
                            r = y + i;
                            c = x + j;
                            //if exceeding range, let it equal to near pixel
                            while (r < 0)
                            { r++; }
                            while (r >= height)
                            { r--; }
                            while (c < 0)
                            { c++; }
                            while (c >= width)
                            { c--; }
                            //************************
                            tmp = inputMat[r, c] * gauss_kernel[i + k_radius, j + k_radius];
                            outputMat[y, x] += (int)tmp;
                        }
                    }
                }
            }
            return outputMat;
        }
        /// <summary>
        /// Temporal differential operator
        /// </summary>
        /// <param name="pre_in"></param>
        /// <param name="cur_in"></param>
        /// <returns></returns>
        protected double DifferentialOperator(double pre_in, double cur_in)
        {
            return cur_in - pre_in;
        }
        #endregion

        #region NETWORK        
        /// <summary>
        /// SNN processing algorithm with STDP online learning
        /// </summary>
        /// <param name="pre_img"></param>
        /// <param name="cur_img"></param>
        /// <param name="t"></param>
        public void SNN_STDP(byte[,,] pre_img, byte[,,] cur_img, int t)
        {
            // Downsampling and Blurring
            int cur_time = t % ONs.GetLength(2);
            int pre_time = (t - 1) % ONs.GetLength(2);
            double[,] ds_pre_img = ResizeMatrix(pre_img, ds_height, ds_width);
            double[,] ds_cur_img = ResizeMatrix(cur_img, ds_height, ds_width);
            double[,] blur_pre_img = GaussBlur(ds_pre_img, blur_kernel_width, blur_kernel, ds_height, ds_width);
            double[,] blur_cur_img = GaussBlur(ds_cur_img, blur_kernel_width, blur_kernel, ds_height, ds_width);
            byte tmpRate = 0;
            // Encoding layer
            for (int y = 0; y < ds_height; y++)
            {
                for (int x = 0; x < ds_width; x++)
                {
                    photoreceptor[y, x] = (int)DifferentialOperator(blur_pre_img[y, x], blur_cur_img[y, x]);
                    ONs[y, x, cur_time] = Halfwave_ON(photoreceptor[y, x], ONs[y, x, pre_time]);
                    OFFs[y, x, cur_time] = Halfwave_OFF(photoreceptor[y, x], OFFs[y, x, pre_time]);
                    PhaseCoding(y, x, ONs, cur_time, ref eventONs);
                    PhaseCoding(y, x, OFFs, cur_time, ref eventOFFs);
                }
            }
            // Spiking interaction layer
            for (int p = 0; p < 8; p++)
            {
                int delay = p - phase_delay;
                if (delay < 0)
                    delay += 8;
                for (int y = 0; y < ds_height; y++)
                {
                    for (int x = 0; x < ds_width; x++)
                    {
                        epsc_ONs[y, x] = CalcNeuronCurrent(y, x, Ws[p], Wexc, eventONs, p, Exc_connection_width, Exc_connection_width, ds_height, ds_width, Vth, cur_time);
                        ipsc_ONs[y, x] = CalcNeuronCurrent(y, x, Ws[delay], Winh, eventONs, delay, Inh_connection_width, Inh_connection_width, ds_height, ds_width, Vth, cur_time);
                        epsc_OFFs[y, x] = CalcNeuronCurrent(y, x, Ws[p], Wexc, eventOFFs, p, Exc_connection_width, Exc_connection_width, ds_height, ds_width, Vth, cur_time);
                        ipsc_OFFs[y, x] = CalcNeuronCurrent(y, x, Ws[delay], Winh, eventOFFs, delay, Inh_connection_width, Inh_connection_width, ds_height, ds_width, Vth, cur_time);
                        spiONs[y, x, p] = Spiking(V_ONs[y, x, delay], epsc_ONs[y, x] - Cinh * ipsc_ONs[y, x], Ws[p], Vth);
                        spiOFFs[y, x, p] = Spiking(V_OFFs[y, x, delay], epsc_OFFs[y, x] - Cinh * ipsc_OFFs[y, x], Ws[p], Vth);
                        V_ONs[y, x, p] = IF_Neuron(V_ONs[y, x, delay], epsc_ONs[y, x] - Cinh * ipsc_ONs[y, x], spiONs[y, x, p], Ws[p], Vth);
                        V_OFFs[y, x, p] = IF_Neuron(V_OFFs[y, x, delay], epsc_OFFs[y, x] - Cinh * ipsc_OFFs[y, x], spiOFFs[y, x, p], Ws[p], Vth);
                    }
                }
                // Output layer
                I_LGMD_ON = CalcNeuronCurrent(Ws[p], Wspa_on, spiONs, p, ds_height, ds_width, Vth);
                I_LGMD_OFF = CalcNeuronCurrent(Ws[p], Wspa_off, spiOFFs, p, ds_height, ds_width, Vth);
                // Arithmetic mean
                I_LGMD = (I_LGMD_ON + I_LGMD_OFF) / 2;
                spiLGMD[p] = Spiking(V_LGMD[delay], I_LGMD, Ws[p], Vth);
                tmpRate += spiLGMD[p];
                V_LGMD[p] = IF_Neuron(V_LGMD[delay], I_LGMD, spiLGMD[p], Ws[p], Vth);
                // STDP learning
                if (postRate + tmpRate > 2)
                {
                    for (int y = 0; y < ds_height; y++)
                    {
                        for (int x = 0; x < ds_width; x++)
                        {
                            Wspa_on[y, x] += (ltp_coe * Wspa_on[y, x] * (1 - Wspa_on[y, x])) * Ws[p];
                            Wspa_off[y, x] += (ltp_coe * Wspa_off[y, x] * (1 - Wspa_off[y, x])) * Ws[p];
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < ds_height; y++)
                    {
                        for (int x = 0; x < ds_width; x++)
                        {
                            Wspa_on[y, x] += WeightModification(spiONs[y, x, p], spiLGMD[p], Wspa_on[y, x]) * Ws[p];
                            Wspa_off[y, x] += WeightModification(spiOFFs[y, x, p], spiLGMD[p], Wspa_off[y, x]) * Ws[p];
                        }
                    }
                }
                // Postsynaptic looming neuron connection
                I_Looming_Neuron = CalcNeuronCurrent(Ws[p], spiLGMD[p], 0);
                // Add responses to lists
                LGMDCurrent.Add(I_LGMD);
                LGMDVoltage.Add(V_LGMD[p]);
                LGMDPulse.Add(spiLGMD[p]);
                LoomingCurrent.Add(I_Looming_Neuron);
                // LIF neuron output
                double k1, k2, k3, k4, dt = (1.0 / this.fps / 8) / lif.iter;
                for (int i = 0; i < lif.iter; i++)
                {
                    int pre = i - 1 < 0 ? i - 1 + lif.iter : i - 1;
                    if (V_Looming_Neuron[pre] == lif.Vact)
                    {
                        V_Looming_Neuron[i] = lif.Vrepo; // Hyperpolarization after a spike
                        if (WARNING == false)
                            WARNING = true;
                        continue;
                    }
                    k1 = -(V_Looming_Neuron[i] - lif.V0) / lif.tau_m + (I_Looming_Neuron * lif.R) / lif.tau_m;
                    k2 = -(V_Looming_Neuron[i] + dt * k1 / 2 - lif.V0) / lif.tau_m + (I_Looming_Neuron * lif.R) / lif.tau_m;
                    k3 = -(V_Looming_Neuron[i] + dt * k2 / 2 - lif.V0) / lif.tau_m + (I_Looming_Neuron * lif.R) / lif.tau_m;
                    k4 = -(V_Looming_Neuron[i] + dt * k3 - lif.V0) / lif.tau_m + (I_Looming_Neuron * lif.R) / lif.tau_m;

                    V_Looming_Neuron[i] = V_Looming_Neuron[pre] + (k1 + 2 * k2 + 2 * k3 + k4) * dt / 6.0;

                    if (V_Looming_Neuron[i] > lif.Vth)
                        V_Looming_Neuron[i] = lif.Vact; // Spiking
                    LoomingVoltage.Add(V_Looming_Neuron[i]);

                    // debug
                    //Console.WriteLine("Frame {0}   Phase {1}    Iter {2}   Voltage {3:F}", t, p, i, V_Looming_Neuron[i]);
                }
            }
            postRate = tmpRate;
        }
        #endregion
    }

    /// <summary>
    /// This struct represents internal states of an leaky integrate-and-fire neuron model
    /// </summary>
    internal struct LIF_Neuron_github
    {
        public int iter;       //iteration time (attention)
        public double step;    //working step in seconds
        public double V0;      //LIF neuron start potential in voltage(mV)
        public double Vth;     //threshold potential(mV)
        public double Vrepo;   //repolarization potential(mV)
        public double Vact;    //action potential(mV)
        public double h;       //the potential iteration time of GF neuron
        public double amp;     //amplifier of current
        public double normal_I;//the normalization factor of current
        public double R;       //resistence
        public double tau_m;   //time constant
    }
}